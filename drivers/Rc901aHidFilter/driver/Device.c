#include <ntddk.h>
#include <wdf.h>
#include <hidport.h>

#include "Device.h"
#include "Sha256.h"

#define RC901A_CAPTURE_POOL_TAG '19CR'

static IO_COMPLETION_ROUTINE Rc901aReportDescriptorCompletion;

static VOID
Rc901aPersistAttachMarker(
    _In_ WDFDEVICE Device
    )
{
    WDFKEY captureKey;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(attachedName, L"Rc901aFilterAttached");

    status = WdfDriverOpenParametersRegistryKey(
        WdfDeviceGetDriver(Device),
        KEY_SET_VALUE,
        WDF_NO_OBJECT_ATTRIBUTES,
        &captureKey
        );
    if (NT_SUCCESS(status)) {
        (void)WdfRegistryAssignULong(captureKey, &attachedName, 1U);
        WdfRegistryClose(captureKey);
    }
}

static VOID
Rc901aQueuePersistWorkItem(
    _In_ PRC901A_DEVICE_CONTEXT Context
    )
{
    if (InterlockedCompareExchange(&Context->WorkItemQueued, 1, 0) == 0) {
        WdfWorkItemEnqueue(Context->PersistWorkItem);
    }
}

_Use_decl_annotations_
NTSTATUS
Rc901aEvtDeviceAdd(
    WDFDRIVER Driver,
    PWDFDEVICE_INIT DeviceInit
    )
{
    WDF_OBJECT_ATTRIBUTES deviceAttributes;
    WDF_OBJECT_ATTRIBUTES childAttributes;
    WDF_WORKITEM_CONFIG workItemConfig;
    WDFDEVICE device;
    PRC901A_DEVICE_CONTEXT context;
    NTSTATUS status;

    UNREFERENCED_PARAMETER(Driver);

    WdfFdoInitSetFilter(DeviceInit);

    status = WdfDeviceInitAssignWdmIrpPreprocessCallback(
        DeviceInit,
        Rc901aEvtWdmIrpPreprocess,
        IRP_MJ_INTERNAL_DEVICE_CONTROL,
        NULL,
        0
        );
    if (!NT_SUCCESS(status)) {
        return status;
    }

    status = WdfDeviceInitAssignWdmIrpPreprocessCallback(
        DeviceInit,
        Rc901aEvtWdmIrpPreprocess,
        IRP_MJ_DEVICE_CONTROL,
        NULL,
        0
        );
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&deviceAttributes, RC901A_DEVICE_CONTEXT);
    status = WdfDeviceCreate(&DeviceInit, &deviceAttributes, &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    context = Rc901aGetDeviceContext(device);
    context->DescriptorLength = 0U;
    context->CaptureStatus = Rc901aCaptureEmpty;
    context->ObservedRequestCount = 0U;
    context->LastMajorFunction = 0U;
    context->LastIoControlCode = 0U;
    context->CompletionCount = 0U;
    context->LastCompletionStatus = STATUS_PENDING;
    context->LastCompletionInformation = 0U;
    context->CaptureGeneration = 0;
    context->WorkItemQueued = 0;

    WDF_OBJECT_ATTRIBUTES_INIT(&childAttributes);
    childAttributes.ParentObject = device;
    status = WdfSpinLockCreate(&childAttributes, &context->CaptureLock);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_WORKITEM_CONFIG_INIT(&workItemConfig, Rc901aEvtPersistCapture);
    WDF_OBJECT_ATTRIBUTES_INIT(&childAttributes);
    childAttributes.ParentObject = device;
    status = WdfWorkItemCreate(
        &workItemConfig,
        &childAttributes,
        &context->PersistWorkItem
        );
    if (NT_SUCCESS(status)) {
        Rc901aPersistAttachMarker(device);
    }

    return status;
}

_Use_decl_annotations_
NTSTATUS
Rc901aEvtWdmIrpPreprocess(
    WDFDEVICE Device,
    PIRP Irp
    )
{
    PRC901A_DEVICE_CONTEXT deviceContext;
    PIO_STACK_LOCATION stack;

    stack = IoGetCurrentIrpStackLocation(Irp);
    deviceContext = Rc901aGetDeviceContext(Device);
    WdfSpinLockAcquire(deviceContext->CaptureLock);
    deviceContext->ObservedRequestCount += 1U;
    deviceContext->LastMajorFunction = (ULONG)stack->MajorFunction;
    deviceContext->LastIoControlCode = stack->Parameters.DeviceIoControl.IoControlCode;
    (void)InterlockedIncrement(&deviceContext->CaptureGeneration);
    WdfSpinLockRelease(deviceContext->CaptureLock);
    Rc901aQueuePersistWorkItem(deviceContext);

    if (stack->Parameters.DeviceIoControl.IoControlCode != IOCTL_HID_GET_REPORT_DESCRIPTOR) {
        IoSkipCurrentIrpStackLocation(Irp);
        return WdfDeviceWdmDispatchPreprocessedIrp(Device, Irp);
    }

    IoCopyCurrentIrpStackLocationToNext(Irp);
    IoSetCompletionRoutine(
        Irp,
        Rc901aReportDescriptorCompletion,
        Device,
        TRUE,
        TRUE,
        TRUE
        );

    return WdfDeviceWdmDispatchPreprocessedIrp(Device, Irp);
}

_Use_decl_annotations_
static NTSTATUS
Rc901aReportDescriptorCompletion(
    PDEVICE_OBJECT DeviceObject,
    PIRP Irp,
    PVOID Context
    )
{
    WDFDEVICE device;
    PRC901A_DEVICE_CONTEXT deviceContext;
    RC901A_CAPTURE_RESULT captureStatus;
    size_t bytesWritten;
    size_t returnedLength;

    UNREFERENCED_PARAMETER(DeviceObject);

    device = (WDFDEVICE)Context;
    deviceContext = Rc901aGetDeviceContext(device);
    bytesWritten = 0U;
    captureStatus = Rc901aCaptureInvalidArgument;
    returnedLength = (size_t)Irp->IoStatus.Information;

    WdfSpinLockAcquire(deviceContext->CaptureLock);
    deviceContext->CompletionCount += 1U;
    deviceContext->LastCompletionStatus = Irp->IoStatus.Status;
    deviceContext->LastCompletionInformation = (ULONG)Irp->IoStatus.Information;
    if (NT_SUCCESS(Irp->IoStatus.Status)) {
        __try {
            captureStatus = Rc901aCopyDescriptor(
                (const unsigned char*)Irp->UserBuffer,
                returnedLength,
                deviceContext->Descriptor,
                sizeof(deviceContext->Descriptor),
                &bytesWritten
                );
        }
        __except (EXCEPTION_EXECUTE_HANDLER) {
            captureStatus = Rc901aCaptureInvalidArgument;
            bytesWritten = 0U;
        }

        deviceContext->DescriptorLength = bytesWritten;
        deviceContext->CaptureStatus = captureStatus;
    }
    (void)InterlockedIncrement(&deviceContext->CaptureGeneration);
    WdfSpinLockRelease(deviceContext->CaptureLock);
    Rc901aQueuePersistWorkItem(deviceContext);

    return STATUS_CONTINUE_COMPLETION;
}

_Use_decl_annotations_
VOID
Rc901aEvtPersistCapture(
    WDFWORKITEM WorkItem
    )
{
    WDFDEVICE device;
    PRC901A_DEVICE_CONTEXT context;
    WDFKEY captureKey;
    unsigned char* descriptor;
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE];
    size_t descriptorLength;
    RC901A_CAPTURE_RESULT captureStatus;
    ULONG observedRequestCount;
    ULONG lastMajorFunction;
    ULONG lastIoControlCode;
    ULONG completionCount;
    NTSTATUS lastCompletionStatus;
    ULONG lastCompletionInformation;
    LONG persistedGeneration;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(descriptorName, L"Rc901aCapturedReportDescriptor");
    DECLARE_CONST_UNICODE_STRING(lengthName, L"Rc901aCapturedReportDescriptorLength");
    DECLARE_CONST_UNICODE_STRING(digestName, L"Rc901aCapturedReportDescriptorSha256");
    DECLARE_CONST_UNICODE_STRING(statusName, L"Rc901aCaptureStatus");
    DECLARE_CONST_UNICODE_STRING(requestCountName, L"Rc901aObservedRequestCount");
    DECLARE_CONST_UNICODE_STRING(majorFunctionName, L"Rc901aLastMajorFunction");
    DECLARE_CONST_UNICODE_STRING(ioControlCodeName, L"Rc901aLastIoControlCode");
    DECLARE_CONST_UNICODE_STRING(completionCountName, L"Rc901aCompletionCount");
    DECLARE_CONST_UNICODE_STRING(completionStatusName, L"Rc901aLastCompletionStatus");
    DECLARE_CONST_UNICODE_STRING(completionInformationName, L"Rc901aLastCompletionInformation");

    device = (WDFDEVICE)WdfWorkItemGetParentObject(WorkItem);
    context = Rc901aGetDeviceContext(device);
    descriptor = (unsigned char*)ExAllocatePool2(
        POOL_FLAG_NON_PAGED,
        RC901A_MAX_REPORT_DESCRIPTOR_SIZE,
        RC901A_CAPTURE_POOL_TAG
        );
    if (descriptor == NULL) {
        InterlockedExchange(&context->WorkItemQueued, 0);
        return;
    }

    WdfSpinLockAcquire(context->CaptureLock);
    descriptorLength = context->DescriptorLength;
    captureStatus = context->CaptureStatus;
    observedRequestCount = context->ObservedRequestCount;
    lastMajorFunction = context->LastMajorFunction;
    lastIoControlCode = context->LastIoControlCode;
    completionCount = context->CompletionCount;
    lastCompletionStatus = context->LastCompletionStatus;
    lastCompletionInformation = context->LastCompletionInformation;
    persistedGeneration = InterlockedCompareExchange(&context->CaptureGeneration, 0, 0);
    if (descriptorLength > 0U && descriptorLength <= RC901A_MAX_REPORT_DESCRIPTOR_SIZE) {
        RtlCopyMemory(descriptor, context->Descriptor, descriptorLength);
    }
    WdfSpinLockRelease(context->CaptureLock);

    status = WdfDriverOpenParametersRegistryKey(
        WdfDeviceGetDriver(device),
        KEY_SET_VALUE,
        WDF_NO_OBJECT_ATTRIBUTES,
        &captureKey
        );
    if (NT_SUCCESS(status)) {
        (void)WdfRegistryAssignULong(captureKey, &statusName, (ULONG)captureStatus);
        (void)WdfRegistryAssignULong(captureKey, &lengthName, (ULONG)descriptorLength);
        (void)WdfRegistryAssignULong(captureKey, &requestCountName, observedRequestCount);
        (void)WdfRegistryAssignULong(captureKey, &majorFunctionName, lastMajorFunction);
        (void)WdfRegistryAssignULong(captureKey, &ioControlCodeName, lastIoControlCode);
        (void)WdfRegistryAssignULong(captureKey, &completionCountName, completionCount);
        (void)WdfRegistryAssignULong(captureKey, &completionStatusName, (ULONG)lastCompletionStatus);
        (void)WdfRegistryAssignULong(captureKey, &completionInformationName, lastCompletionInformation);

        if (captureStatus == Rc901aCaptureSuccess &&
            descriptorLength > 0U &&
            Rc901aComputeSha256(descriptor, descriptorLength, digest) != 0) {
            (void)WdfRegistryAssignValue(
                captureKey,
                &descriptorName,
                REG_BINARY,
                (ULONG)descriptorLength,
                descriptor
            );
            (void)WdfRegistryAssignValue(
                captureKey,
                &digestName,
                REG_BINARY,
                RC901A_SHA256_DIGEST_SIZE,
                digest
            );
        }

        WdfRegistryClose(captureKey);
    }

    ExFreePoolWithTag(descriptor, RC901A_CAPTURE_POOL_TAG);
    InterlockedExchange(&context->WorkItemQueued, 0);

    if (InterlockedCompareExchange(&context->CaptureGeneration, 0, 0) != persistedGeneration) {
        Rc901aQueuePersistWorkItem(context);
    }
}
