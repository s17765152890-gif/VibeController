#include <ntddk.h>
#include <wdf.h>
#include <hidport.h>

#include "Device.h"
#include "Sha256.h"

#define RC901A_CAPTURE_POOL_TAG '19CR'
#define RC901A_UMDF_TRANSPORT_IOCTL \
    CTL_CODE(FILE_DEVICE_BLUETOOTH, 0x489, METHOD_BUFFERED, FILE_ANY_ACCESS)

static IO_COMPLETION_ROUTINE Rc901aReportDescriptorCompletion;
static IO_COMPLETION_ROUTINE Rc901aUmdfTransportCompletion;

typedef struct _RC901A_UMDF_COMPLETION_CONTEXT {
    WDFDEVICE Device;
    ULONG OutputBufferLength;
} RC901A_UMDF_COMPLETION_CONTEXT, *PRC901A_UMDF_COMPLETION_CONTEXT;

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
    context->ObservedStoredCount = 0U;
    context->LastMajorFunction = 0U;
    context->LastIoControlCode = 0U;
    context->CompletionCount = 0U;
    context->LastCompletionStatus = STATUS_PENDING;
    context->LastCompletionInformation = 0U;
    context->ObservedInputBufferLength = 0U;
    context->ObservedOutputBufferLength = 0U;
    context->ObservedInputCapturedLength = 0U;
    context->ObservedCompletionCapturedLength = 0U;
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
    PIO_COMPLETION_ROUTINE completionRoutine;
    PVOID completionContext;

    stack = IoGetCurrentIrpStackLocation(Irp);
    deviceContext = Rc901aGetDeviceContext(Device);
    completionRoutine = NULL;
    completionContext = NULL;
    WdfSpinLockAcquire(deviceContext->CaptureLock);
    deviceContext->ObservedRequestCount += 1U;
    if (deviceContext->ObservedStoredCount < RC901A_MAX_OBSERVED_REQUESTS) {
        ULONG observedIndex;

        observedIndex = deviceContext->ObservedStoredCount;
        deviceContext->ObservedMajorFunctions[observedIndex] = (ULONG)stack->MajorFunction;
        deviceContext->ObservedIoControlCodes[observedIndex] =
            stack->Parameters.DeviceIoControl.IoControlCode;
        deviceContext->ObservedStoredCount += 1U;
    }
    deviceContext->LastMajorFunction = (ULONG)stack->MajorFunction;
    deviceContext->LastIoControlCode = stack->Parameters.DeviceIoControl.IoControlCode;
    if (stack->Parameters.DeviceIoControl.IoControlCode == RC901A_UMDF_TRANSPORT_IOCTL) {
        ULONG inputLength;
        ULONG inputCopyLength;

        inputLength = stack->Parameters.DeviceIoControl.InputBufferLength;
        deviceContext->ObservedInputBufferLength = inputLength;
        deviceContext->ObservedOutputBufferLength =
            stack->Parameters.DeviceIoControl.OutputBufferLength;
        deviceContext->ObservedInputCapturedLength = 0U;
        deviceContext->ObservedCompletionCapturedLength = 0U;
        inputCopyLength = inputLength;
        if (inputCopyLength > RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE) {
            inputCopyLength = RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE;
        }

        if (inputCopyLength > 0U && Irp->AssociatedIrp.SystemBuffer != NULL) {
            __try {
                RtlCopyMemory(
                    deviceContext->ObservedInputBuffer,
                    Irp->AssociatedIrp.SystemBuffer,
                    inputCopyLength
                    );
                deviceContext->ObservedInputCapturedLength = inputCopyLength;
            }
            __except (EXCEPTION_EXECUTE_HANDLER) {
                deviceContext->ObservedInputCapturedLength = 0U;
            }
        }
    }
    (void)InterlockedIncrement(&deviceContext->CaptureGeneration);
    WdfSpinLockRelease(deviceContext->CaptureLock);
    Rc901aQueuePersistWorkItem(deviceContext);

    if (stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_HID_GET_REPORT_DESCRIPTOR) {
        completionRoutine = Rc901aReportDescriptorCompletion;
        completionContext = Device;
    }
    else if (stack->Parameters.DeviceIoControl.IoControlCode == RC901A_UMDF_TRANSPORT_IOCTL) {
        PRC901A_UMDF_COMPLETION_CONTEXT umdfContext;

        umdfContext = (PRC901A_UMDF_COMPLETION_CONTEXT)ExAllocatePool2(
            POOL_FLAG_NON_PAGED,
            sizeof(*umdfContext),
            RC901A_CAPTURE_POOL_TAG
            );
        if (umdfContext != NULL) {
            WdfObjectReference(Device);
            umdfContext->Device = Device;
            umdfContext->OutputBufferLength =
                stack->Parameters.DeviceIoControl.OutputBufferLength;
            completionRoutine = Rc901aUmdfTransportCompletion;
            completionContext = umdfContext;
        }
    }

    if (completionRoutine == NULL) {
        IoSkipCurrentIrpStackLocation(Irp);
        return WdfDeviceWdmDispatchPreprocessedIrp(Device, Irp);
    }

    IoCopyCurrentIrpStackLocationToNext(Irp);
    IoSetCompletionRoutine(
        Irp,
        completionRoutine,
        completionContext,
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
static NTSTATUS
Rc901aUmdfTransportCompletion(
    PDEVICE_OBJECT DeviceObject,
    PIRP Irp,
    PVOID Context
    )
{
    PRC901A_UMDF_COMPLETION_CONTEXT completionContext;
    WDFDEVICE device;
    PRC901A_DEVICE_CONTEXT deviceContext;
    ULONG outputCopyLength;

    UNREFERENCED_PARAMETER(DeviceObject);

    completionContext = (PRC901A_UMDF_COMPLETION_CONTEXT)Context;
    device = completionContext->Device;
    deviceContext = Rc901aGetDeviceContext(device);
    outputCopyLength = 0U;
    if (NT_SUCCESS(Irp->IoStatus.Status) &&
        Irp->IoStatus.Information > 0U &&
        Irp->AssociatedIrp.SystemBuffer != NULL) {
        outputCopyLength = (ULONG)Irp->IoStatus.Information;
        if (outputCopyLength > completionContext->OutputBufferLength) {
            outputCopyLength = completionContext->OutputBufferLength;
        }
        if (outputCopyLength > RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE) {
            outputCopyLength = RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE;
        }
    }

    WdfSpinLockAcquire(deviceContext->CaptureLock);
    deviceContext->CompletionCount += 1U;
    deviceContext->LastCompletionStatus = Irp->IoStatus.Status;
    deviceContext->LastCompletionInformation = (ULONG)Irp->IoStatus.Information;
    deviceContext->ObservedCompletionCapturedLength = 0U;
    if (outputCopyLength > 0U) {
        __try {
            RtlCopyMemory(
                deviceContext->ObservedCompletionBuffer,
                Irp->AssociatedIrp.SystemBuffer,
                outputCopyLength
                );
            deviceContext->ObservedCompletionCapturedLength = outputCopyLength;
        }
        __except (EXCEPTION_EXECUTE_HANDLER) {
            deviceContext->ObservedCompletionCapturedLength = 0U;
        }
    }
    (void)InterlockedIncrement(&deviceContext->CaptureGeneration);
    WdfSpinLockRelease(deviceContext->CaptureLock);
    Rc901aQueuePersistWorkItem(deviceContext);

    WdfObjectDereference(device);
    ExFreePoolWithTag(completionContext, RC901A_CAPTURE_POOL_TAG);
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
    unsigned char* diagnosticBuffers;
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE];
    size_t descriptorLength;
    RC901A_CAPTURE_RESULT captureStatus;
    ULONG observedRequestCount;
    ULONG observedStoredCount;
    ULONG observedMajorFunctions[RC901A_MAX_OBSERVED_REQUESTS];
    ULONG observedIoControlCodes[RC901A_MAX_OBSERVED_REQUESTS];
    ULONG lastMajorFunction;
    ULONG lastIoControlCode;
    ULONG completionCount;
    NTSTATUS lastCompletionStatus;
    ULONG lastCompletionInformation;
    ULONG observedInputBufferLength;
    ULONG observedOutputBufferLength;
    ULONG observedInputCapturedLength;
    ULONG observedCompletionCapturedLength;
    LONG persistedGeneration;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(descriptorName, L"Rc901aCapturedReportDescriptor");
    DECLARE_CONST_UNICODE_STRING(lengthName, L"Rc901aCapturedReportDescriptorLength");
    DECLARE_CONST_UNICODE_STRING(digestName, L"Rc901aCapturedReportDescriptorSha256");
    DECLARE_CONST_UNICODE_STRING(statusName, L"Rc901aCaptureStatus");
    DECLARE_CONST_UNICODE_STRING(requestCountName, L"Rc901aObservedRequestCount");
    DECLARE_CONST_UNICODE_STRING(majorFunctionsName, L"Rc901aObservedMajorFunctions");
    DECLARE_CONST_UNICODE_STRING(ioControlCodesName, L"Rc901aObservedIoControlCodes");
    DECLARE_CONST_UNICODE_STRING(majorFunctionName, L"Rc901aLastMajorFunction");
    DECLARE_CONST_UNICODE_STRING(ioControlCodeName, L"Rc901aLastIoControlCode");
    DECLARE_CONST_UNICODE_STRING(completionCountName, L"Rc901aCompletionCount");
    DECLARE_CONST_UNICODE_STRING(completionStatusName, L"Rc901aLastCompletionStatus");
    DECLARE_CONST_UNICODE_STRING(completionInformationName, L"Rc901aLastCompletionInformation");
    DECLARE_CONST_UNICODE_STRING(inputBufferLengthName, L"Rc901aObservedInputBufferLength");
    DECLARE_CONST_UNICODE_STRING(outputBufferLengthName, L"Rc901aObservedOutputBufferLength");
    DECLARE_CONST_UNICODE_STRING(inputBufferName, L"Rc901aObservedInputBuffer");
    DECLARE_CONST_UNICODE_STRING(completionBufferName, L"Rc901aObservedCompletionBuffer");
    DECLARE_CONST_UNICODE_STRING(completionBufferLengthName, L"Rc901aObservedCompletionBufferLength");

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
    diagnosticBuffers = (unsigned char*)ExAllocatePool2(
        POOL_FLAG_NON_PAGED,
        RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE * 2U,
        RC901A_CAPTURE_POOL_TAG
        );
    if (diagnosticBuffers == NULL) {
        ExFreePoolWithTag(descriptor, RC901A_CAPTURE_POOL_TAG);
        InterlockedExchange(&context->WorkItemQueued, 0);
        return;
    }

    WdfSpinLockAcquire(context->CaptureLock);
    descriptorLength = context->DescriptorLength;
    captureStatus = context->CaptureStatus;
    observedRequestCount = context->ObservedRequestCount;
    observedStoredCount = context->ObservedStoredCount;
    if (observedStoredCount > 0U) {
        RtlCopyMemory(
            observedMajorFunctions,
            context->ObservedMajorFunctions,
            observedStoredCount * sizeof(ULONG)
            );
        RtlCopyMemory(
            observedIoControlCodes,
            context->ObservedIoControlCodes,
            observedStoredCount * sizeof(ULONG)
            );
    }
    lastMajorFunction = context->LastMajorFunction;
    lastIoControlCode = context->LastIoControlCode;
    completionCount = context->CompletionCount;
    lastCompletionStatus = context->LastCompletionStatus;
    lastCompletionInformation = context->LastCompletionInformation;
    observedInputBufferLength = context->ObservedInputBufferLength;
    observedOutputBufferLength = context->ObservedOutputBufferLength;
    observedInputCapturedLength = context->ObservedInputCapturedLength;
    observedCompletionCapturedLength = context->ObservedCompletionCapturedLength;
    if (observedInputCapturedLength > 0U) {
        RtlCopyMemory(
            diagnosticBuffers,
            context->ObservedInputBuffer,
            observedInputCapturedLength
            );
    }
    if (observedCompletionCapturedLength > 0U) {
        RtlCopyMemory(
            diagnosticBuffers + RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE,
            context->ObservedCompletionBuffer,
            observedCompletionCapturedLength
            );
    }
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
        if (observedStoredCount > 0U) {
            (void)WdfRegistryAssignValue(
                captureKey,
                &majorFunctionsName,
                REG_BINARY,
                (ULONG)(observedStoredCount * sizeof(ULONG)),
                observedMajorFunctions
                );
            (void)WdfRegistryAssignValue(
                captureKey,
                &ioControlCodesName,
                REG_BINARY,
                (ULONG)(observedStoredCount * sizeof(ULONG)),
                observedIoControlCodes
                );
        }
        (void)WdfRegistryAssignULong(captureKey, &majorFunctionName, lastMajorFunction);
        (void)WdfRegistryAssignULong(captureKey, &ioControlCodeName, lastIoControlCode);
        (void)WdfRegistryAssignULong(captureKey, &completionCountName, completionCount);
        (void)WdfRegistryAssignULong(captureKey, &completionStatusName, (ULONG)lastCompletionStatus);
        (void)WdfRegistryAssignULong(captureKey, &completionInformationName, lastCompletionInformation);
        (void)WdfRegistryAssignULong(captureKey, &inputBufferLengthName, observedInputBufferLength);
        (void)WdfRegistryAssignULong(captureKey, &outputBufferLengthName, observedOutputBufferLength);
        (void)WdfRegistryAssignULong(
            captureKey,
            &completionBufferLengthName,
            observedCompletionCapturedLength
            );
        if (observedInputCapturedLength > 0U) {
            (void)WdfRegistryAssignValue(
                captureKey,
                &inputBufferName,
                REG_BINARY,
                observedInputCapturedLength,
                diagnosticBuffers
                );
        }
        if (observedCompletionCapturedLength > 0U) {
            (void)WdfRegistryAssignValue(
                captureKey,
                &completionBufferName,
                REG_BINARY,
                observedCompletionCapturedLength,
                diagnosticBuffers + RC901A_MAX_DIAGNOSTIC_BUFFER_SIZE
                );
        }

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

    ExFreePoolWithTag(diagnosticBuffers, RC901A_CAPTURE_POOL_TAG);
    ExFreePoolWithTag(descriptor, RC901A_CAPTURE_POOL_TAG);
    InterlockedExchange(&context->WorkItemQueued, 0);

    if (InterlockedCompareExchange(&context->CaptureGeneration, 0, 0) != persistedGeneration) {
        Rc901aQueuePersistWorkItem(context);
    }
}
