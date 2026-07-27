#include <windows.h>
#include <wdf.h>
#include <hidport.h>
#include <string.h>

#include "DescriptorCapture.h"
#include "InputReportCapture.h"
#include "Rc901aCaptureProtocol.h"
#include "Sha256.h"

typedef struct _RC901A_UMDF_DEVICE_CONTEXT {
    WDFSPINLOCK CaptureLock;
    WDFWORKITEM PersistWorkItem;
    unsigned char Descriptor[RC901A_MAX_REPORT_DESCRIPTOR_SIZE];
    size_t DescriptorLength;
    RC901A_CAPTURE_RESULT CaptureStatus;
    RC901A_DESCRIPTOR_REPAIR_RESULT RepairStatus;
    NTSTATUS LowerStatus;
    ULONG_PTR LowerInformation;
    ULONG DescriptorRequestCount;
    ULONG LastIoControlCode;
    RC901A_INPUT_REPORT_HISTORY InputReports;
    NTSTATUS LastInputStatus;
    ULONG_PTR LastInputInformation;
    volatile LONG CaptureGeneration;
    volatile LONG WorkItemQueued;
} RC901A_UMDF_DEVICE_CONTEXT, *PRC901A_UMDF_DEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(
    RC901A_UMDF_DEVICE_CONTEXT,
    Rc901aGetUmdfDeviceContext
    )

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD Rc901aEvtUmdfDeviceAdd;
EVT_WDF_IO_QUEUE_IO_DEFAULT Rc901aEvtIoDefault;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL Rc901aEvtIoDeviceControl;
EVT_WDF_REQUEST_COMPLETION_ROUTINE Rc901aEvtDescriptorComplete;
EVT_WDF_REQUEST_COMPLETION_ROUTINE Rc901aEvtInputReportComplete;
EVT_WDF_WORKITEM Rc901aEvtPersistDescriptor;

static VOID
Rc901aForwardAndForget(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request
    )
{
    WDF_REQUEST_SEND_OPTIONS options;
    WDFDEVICE device;

    device = WdfIoQueueGetDevice(Queue);
    WdfRequestFormatRequestUsingCurrentType(Request);
    WDF_REQUEST_SEND_OPTIONS_INIT(
        &options,
        WDF_REQUEST_SEND_OPTION_SEND_AND_FORGET
        );

    if (!WdfRequestSend(
            Request,
            WdfDeviceGetIoTarget(device),
            &options)) {
        WdfRequestComplete(Request, WdfRequestGetStatus(Request));
    }
}

static VOID
Rc901aQueuePersistWorkItem(
    _In_ PRC901A_UMDF_DEVICE_CONTEXT Context
    )
{
    if (InterlockedCompareExchange(&Context->WorkItemQueued, 1, 0) == 0) {
        WdfWorkItemEnqueue(Context->PersistWorkItem);
    }
}

static VOID
Rc901aCompleteInputSnapshotRequest(
    _In_ PRC901A_UMDF_DEVICE_CONTEXT Context,
    _In_ WDFREQUEST Request
    )
{
    RC901A_INPUT_REPORT_SNAPSHOT* snapshot;
    PVOID outputBuffer;
    size_t outputCapacity;
    size_t bytesWritten;
    RC901A_INPUT_REPORT_CAPTURE_RESULT captureResult;
    NTSTATUS status;

    outputBuffer = NULL;
    outputCapacity = 0U;
    bytesWritten = 0U;
    status = WdfRequestRetrieveOutputBuffer(
        Request,
        sizeof(RC901A_INPUT_REPORT_SNAPSHOT),
        &outputBuffer,
        &outputCapacity
        );
    if (!NT_SUCCESS(status)) {
        WdfRequestCompleteWithInformation(Request, status, 0U);
        return;
    }

    snapshot = (RC901A_INPUT_REPORT_SNAPSHOT*)outputBuffer;
    WdfSpinLockAcquire(Context->CaptureLock);
    captureResult = Rc901aBuildInputReportSnapshot(
        &Context->InputReports,
        snapshot,
        outputCapacity,
        &bytesWritten
        );
    WdfSpinLockRelease(Context->CaptureLock);

    switch (captureResult) {
    case Rc901aInputReportCaptureSuccess:
        status = STATUS_SUCCESS;
        break;
    case Rc901aInputReportCaptureDestinationTooSmall:
        status = STATUS_BUFFER_TOO_SMALL;
        bytesWritten = 0U;
        break;
    default:
        status = STATUS_INVALID_PARAMETER;
        bytesWritten = 0U;
        break;
    }

    WdfRequestCompleteWithInformation(
        Request,
        status,
        NT_SUCCESS(status) ? bytesWritten : 0U
        );
}

static VOID
Rc901aRecordDescriptorResult(
    _In_ PRC901A_UMDF_DEVICE_CONTEXT Context,
    _In_ NTSTATUS LowerStatus,
    _In_ ULONG_PTR LowerInformation,
    _Inout_updates_bytes_opt_(BufferLength) unsigned char* Buffer,
    _In_ size_t BufferLength
    )
{
    RC901A_CAPTURE_RESULT captureStatus;
    RC901A_DESCRIPTOR_REPAIR_RESULT repairStatus;
    size_t bytesWritten;

    captureStatus = Rc901aCaptureEmpty;
    repairStatus = Rc901aDescriptorRepairNotApplicable;
    bytesWritten = 0U;

    WdfSpinLockAcquire(Context->CaptureLock);
    if (Buffer != NULL && BufferLength > 0U) {
        captureStatus = Rc901aCopyDescriptor(
            Buffer,
            BufferLength,
            Context->Descriptor,
            sizeof(Context->Descriptor),
            &bytesWritten
            );
        if (captureStatus == Rc901aCaptureSuccess) {
            repairStatus = Rc901aRepairReportDescriptor(
                Buffer,
                BufferLength
                );
        }
    }
    else if (NT_SUCCESS(LowerStatus) && LowerInformation > 0U) {
        captureStatus = Rc901aCaptureInvalidArgument;
    }

    Context->DescriptorLength = bytesWritten;
    Context->CaptureStatus = captureStatus;
    Context->RepairStatus = repairStatus;
    Context->LowerStatus = LowerStatus;
    Context->LowerInformation = LowerInformation;
    Context->DescriptorRequestCount += 1U;
    Context->LastIoControlCode = IOCTL_HID_GET_REPORT_DESCRIPTOR;
    (void)InterlockedIncrement(&Context->CaptureGeneration);
    WdfSpinLockRelease(Context->CaptureLock);
    Rc901aQueuePersistWorkItem(Context);
}

static VOID
Rc901aRecordInputReportResult(
    _In_ PRC901A_UMDF_DEVICE_CONTEXT Context,
    _In_ ULONG IoControlCode,
    _In_ NTSTATUS LowerStatus,
    _In_ ULONG_PTR LowerInformation,
    _In_reads_bytes_opt_(BufferLength) const unsigned char* Buffer,
    _In_ size_t BufferLength
    )
{
    WdfSpinLockAcquire(Context->CaptureLock);
    if (NT_SUCCESS(LowerStatus) &&
        LowerInformation > 0U &&
        Buffer != NULL &&
        BufferLength > 0U) {
        (void)Rc901aRecordInputReport(
            &Context->InputReports,
            (uint32_t)IoControlCode,
            Buffer,
            BufferLength
            );
    }
    Context->LastInputStatus = LowerStatus;
    Context->LastInputInformation = LowerInformation;
    (void)InterlockedIncrement(&Context->CaptureGeneration);
    WdfSpinLockRelease(Context->CaptureLock);
    Rc901aQueuePersistWorkItem(Context);
}

static VOID
Rc901aPersistAttachMarker(
    _In_ WDFDEVICE Device
    )
{
    WDFKEY captureKey;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(attachedName, L"Rc901aUmdfCaptureAttached");
    DECLARE_CONST_UNICODE_STRING(lengthName, L"Rc901aCapturedReportDescriptorLength");
    DECLARE_CONST_UNICODE_STRING(descriptorName, L"Rc901aCapturedReportDescriptor");
    DECLARE_CONST_UNICODE_STRING(digestName, L"Rc901aCapturedReportDescriptorSha256");
    DECLARE_CONST_UNICODE_STRING(repairStatusName, L"Rc901aDescriptorRepairStatus");
    DECLARE_CONST_UNICODE_STRING(inputHistoryName, L"Rc901aInputReportHistory");
    DECLARE_CONST_UNICODE_STRING(inputCountName, L"Rc901aInputReportCount");
    DECLARE_CONST_UNICODE_STRING(inputTotalName, L"Rc901aInputReportTotal");
    ULONGLONG inputTotal;

    inputTotal = 0U;

    status = WdfDriverOpenParametersRegistryKey(
        WdfDeviceGetDriver(Device),
        KEY_READ | KEY_SET_VALUE,
        WDF_NO_OBJECT_ATTRIBUTES,
        &captureKey
        );
    if (NT_SUCCESS(status)) {
        (void)WdfRegistryAssignULong(captureKey, &attachedName, 1U);
        (void)WdfRegistryAssignULong(captureKey, &lengthName, 0U);
        (void)WdfRegistryAssignULong(
            captureKey,
            &repairStatusName,
            (ULONG)Rc901aDescriptorRepairNotApplicable
            );
        (void)WdfRegistryAssignULong(captureKey, &inputCountName, 0U);
        (void)WdfRegistryAssignValue(
            captureKey,
            &inputTotalName,
            REG_QWORD,
            (ULONG)sizeof(inputTotal),
            &inputTotal
            );
        (void)WdfRegistryRemoveValue(captureKey, &descriptorName);
        (void)WdfRegistryRemoveValue(captureKey, &digestName);
        (void)WdfRegistryRemoveValue(captureKey, &inputHistoryName);
        WdfRegistryClose(captureKey);
    }
}

_Use_decl_annotations_
NTSTATUS
DriverEntry(
    PDRIVER_OBJECT DriverObject,
    PUNICODE_STRING RegistryPath
    )
{
    WDF_DRIVER_CONFIG config;
    WDF_OBJECT_ATTRIBUTES attributes;

    WDF_DRIVER_CONFIG_INIT(&config, Rc901aEvtUmdfDeviceAdd);
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);

    return WdfDriverCreate(
        DriverObject,
        RegistryPath,
        &attributes,
        &config,
        WDF_NO_HANDLE
        );
}

_Use_decl_annotations_
NTSTATUS
Rc901aEvtUmdfDeviceAdd(
    WDFDRIVER Driver,
    PWDFDEVICE_INIT DeviceInit
    )
{
    WDF_OBJECT_ATTRIBUTES deviceAttributes;
    WDF_OBJECT_ATTRIBUTES childAttributes;
    WDF_WORKITEM_CONFIG workItemConfig;
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDFDEVICE device;
    PRC901A_UMDF_DEVICE_CONTEXT context;
    NTSTATUS status;

    UNREFERENCED_PARAMETER(Driver);

    WdfFdoInitSetFilter(DeviceInit);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(
        &deviceAttributes,
        RC901A_UMDF_DEVICE_CONTEXT
        );
    deviceAttributes.ExecutionLevel = WdfExecutionLevelPassive;
    status = WdfDeviceCreate(&DeviceInit, &deviceAttributes, &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    status = WdfDeviceCreateDeviceInterface(
        device,
        &GUID_DEVINTERFACE_VIBECONTROLLER_RC901A_CAPTURE,
        NULL
        );
    if (!NT_SUCCESS(status)) {
        return status;
    }

    context = Rc901aGetUmdfDeviceContext(device);
    context->DescriptorLength = 0U;
    context->CaptureStatus = Rc901aCaptureEmpty;
    context->RepairStatus = Rc901aDescriptorRepairNotApplicable;
    context->LowerStatus = STATUS_PENDING;
    context->LowerInformation = 0U;
    context->DescriptorRequestCount = 0U;
    context->LastIoControlCode = 0U;
    Rc901aInitializeInputReportHistory(&context->InputReports);
    context->LastInputStatus = STATUS_PENDING;
    context->LastInputInformation = 0U;
    context->CaptureGeneration = 0;
    context->WorkItemQueued = 0;

    WDF_OBJECT_ATTRIBUTES_INIT(&childAttributes);
    childAttributes.ParentObject = device;
    status = WdfSpinLockCreate(&childAttributes, &context->CaptureLock);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_WORKITEM_CONFIG_INIT(&workItemConfig, Rc901aEvtPersistDescriptor);
    WDF_OBJECT_ATTRIBUTES_INIT(&childAttributes);
    childAttributes.ParentObject = device;
    status = WdfWorkItemCreate(
        &workItemConfig,
        &childAttributes,
        &context->PersistWorkItem
        );
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(
        &queueConfig,
        WdfIoQueueDispatchParallel
        );
    queueConfig.EvtIoDefault = Rc901aEvtIoDefault;
    // hidumdf converts HIDClass INTERNAL IOCTLs to DEVICE_CONTROL before UMDF.
    queueConfig.EvtIoDeviceControl = Rc901aEvtIoDeviceControl;
    status = WdfIoQueueCreate(
        device,
        &queueConfig,
        WDF_NO_OBJECT_ATTRIBUTES,
        WDF_NO_HANDLE
        );
    if (NT_SUCCESS(status)) {
        Rc901aPersistAttachMarker(device);
    }

    return status;
}

_Use_decl_annotations_
VOID
Rc901aEvtIoDefault(
    WDFQUEUE Queue,
    WDFREQUEST Request
    )
{
    Rc901aForwardAndForget(Queue, Request);
}

_Use_decl_annotations_
VOID
Rc901aEvtIoDeviceControl(
    WDFQUEUE Queue,
    WDFREQUEST Request,
    size_t OutputBufferLength,
    size_t InputBufferLength,
    ULONG IoControlCode
    )
{
    WDFDEVICE device;
    PRC901A_UMDF_DEVICE_CONTEXT context;
    PFN_WDF_REQUEST_COMPLETION_ROUTINE completionRoutine;

    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    device = WdfIoQueueGetDevice(Queue);
    context = Rc901aGetUmdfDeviceContext(device);
    if (IoControlCode == IOCTL_RC901A_GET_INPUT_REPORTS) {
        Rc901aCompleteInputSnapshotRequest(context, Request);
        return;
    }

    if (IoControlCode != IOCTL_HID_GET_REPORT_DESCRIPTOR &&
        IoControlCode != IOCTL_HID_READ_REPORT &&
        IoControlCode != IOCTL_HID_GET_INPUT_REPORT) {
        Rc901aForwardAndForget(Queue, Request);
        return;
    }

    completionRoutine =
        IoControlCode == IOCTL_HID_GET_REPORT_DESCRIPTOR
        ? Rc901aEvtDescriptorComplete
        : Rc901aEvtInputReportComplete;
    WdfObjectReference(device);
    WdfRequestFormatRequestUsingCurrentType(Request);
    WdfRequestSetCompletionRoutine(
        Request,
        completionRoutine,
        device
        );

    if (!WdfRequestSend(
            Request,
            WdfDeviceGetIoTarget(device),
            WDF_NO_SEND_OPTIONS)) {
        NTSTATUS status;

        status = WdfRequestGetStatus(Request);
        if (IoControlCode == IOCTL_HID_GET_REPORT_DESCRIPTOR) {
            Rc901aRecordDescriptorResult(context, status, 0U, NULL, 0U);
        }
        else {
            Rc901aRecordInputReportResult(
                context,
                IoControlCode,
                status,
                0U,
                NULL,
                0U
                );
        }
        WdfObjectDereference(device);
        WdfRequestCompleteWithInformation(Request, status, 0U);
    }
}

_Use_decl_annotations_
VOID
Rc901aEvtDescriptorComplete(
    WDFREQUEST Request,
    WDFIOTARGET Target,
    PWDF_REQUEST_COMPLETION_PARAMS Params,
    WDFCONTEXT Context
    )
{
    WDFDEVICE device;
    PRC901A_UMDF_DEVICE_CONTEXT deviceContext;
    NTSTATUS lowerStatus;
    ULONG_PTR lowerInformation;
    PVOID outputBuffer;
    size_t outputCapacity;
    size_t copyLength;
    NTSTATUS bufferStatus;

    UNREFERENCED_PARAMETER(Target);

    device = (WDFDEVICE)Context;
    deviceContext = Rc901aGetUmdfDeviceContext(device);
    lowerStatus = Params->IoStatus.Status;
    lowerInformation = Params->IoStatus.Information;
    outputBuffer = NULL;
    outputCapacity = 0U;
    copyLength = 0U;
    bufferStatus = STATUS_UNSUCCESSFUL;

    if (NT_SUCCESS(lowerStatus) && lowerInformation > 0U) {
        bufferStatus = WdfRequestRetrieveOutputBuffer(
            Request,
            1U,
            &outputBuffer,
            &outputCapacity
            );
        if (NT_SUCCESS(bufferStatus) && outputBuffer != NULL) {
            copyLength = (size_t)lowerInformation;
            if (copyLength > outputCapacity) {
                copyLength = outputCapacity;
            }
        }
    }

    if (NT_SUCCESS(bufferStatus) && outputBuffer != NULL) {
        Rc901aRecordDescriptorResult(
            deviceContext,
            lowerStatus,
            lowerInformation,
            (unsigned char*)outputBuffer,
            copyLength
            );
    }
    else {
        Rc901aRecordDescriptorResult(
            deviceContext,
            lowerStatus,
            lowerInformation,
            NULL,
            0U
            );
    }

    WdfRequestCompleteWithInformation(
        Request,
        lowerStatus,
        lowerInformation
        );
    WdfObjectDereference(device);
}

_Use_decl_annotations_
VOID
Rc901aEvtInputReportComplete(
    WDFREQUEST Request,
    WDFIOTARGET Target,
    PWDF_REQUEST_COMPLETION_PARAMS Params,
    WDFCONTEXT Context
    )
{
    WDFDEVICE device;
    PRC901A_UMDF_DEVICE_CONTEXT deviceContext;
    NTSTATUS lowerStatus;
    ULONG_PTR lowerInformation;
    ULONG ioControlCode;
    PVOID outputBuffer;
    size_t outputCapacity;
    size_t copyLength;
    NTSTATUS bufferStatus;

    UNREFERENCED_PARAMETER(Target);

    device = (WDFDEVICE)Context;
    deviceContext = Rc901aGetUmdfDeviceContext(device);
    lowerStatus = Params->IoStatus.Status;
    lowerInformation = Params->IoStatus.Information;
    ioControlCode = Params->Parameters.Ioctl.IoControlCode;
    outputBuffer = NULL;
    outputCapacity = 0U;
    copyLength = 0U;
    bufferStatus = STATUS_UNSUCCESSFUL;

    if (NT_SUCCESS(lowerStatus) && lowerInformation > 0U) {
        bufferStatus = WdfRequestRetrieveOutputBuffer(
            Request,
            1U,
            &outputBuffer,
            &outputCapacity
            );
        if (NT_SUCCESS(bufferStatus) && outputBuffer != NULL) {
            copyLength = (size_t)lowerInformation;
            if (copyLength > outputCapacity) {
                copyLength = outputCapacity;
            }
        }
    }

    Rc901aRecordInputReportResult(
        deviceContext,
        ioControlCode,
        lowerStatus,
        lowerInformation,
        NT_SUCCESS(bufferStatus)
            ? (const unsigned char*)outputBuffer
            : NULL,
        copyLength
        );

    WdfRequestCompleteWithInformation(
        Request,
        lowerStatus,
        lowerInformation
        );
    WdfObjectDereference(device);
}

_Use_decl_annotations_
VOID
Rc901aEvtPersistDescriptor(
    WDFWORKITEM WorkItem
    )
{
    WDFDEVICE device;
    PRC901A_UMDF_DEVICE_CONTEXT context;
    WDFKEY captureKey;
    unsigned char descriptor[RC901A_MAX_REPORT_DESCRIPTOR_SIZE];
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE];
    RC901A_INPUT_REPORT_RECORD inputReports[
        RC901A_INPUT_REPORT_HISTORY_CAPACITY];
    size_t descriptorLength;
    size_t inputReportCount;
    ULONGLONG inputReportTotal;
    RC901A_CAPTURE_RESULT captureStatus;
    RC901A_DESCRIPTOR_REPAIR_RESULT repairStatus;
    NTSTATUS lowerStatus;
    ULONG_PTR lowerInformation;
    ULONGLONG lowerInformation64;
    ULONG requestCount;
    ULONG lastIoControlCode;
    NTSTATUS lastInputStatus;
    ULONG_PTR lastInputInformation;
    ULONGLONG lastInputInformation64;
    LONG persistedGeneration;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(descriptorName, L"Rc901aCapturedReportDescriptor");
    DECLARE_CONST_UNICODE_STRING(lengthName, L"Rc901aCapturedReportDescriptorLength");
    DECLARE_CONST_UNICODE_STRING(digestName, L"Rc901aCapturedReportDescriptorSha256");
    DECLARE_CONST_UNICODE_STRING(captureStatusName, L"Rc901aCaptureStatus");
    DECLARE_CONST_UNICODE_STRING(repairStatusName, L"Rc901aDescriptorRepairStatus");
    DECLARE_CONST_UNICODE_STRING(completionStatusName, L"Rc901aDescriptorCompletionStatus");
    DECLARE_CONST_UNICODE_STRING(completionInformationName, L"Rc901aDescriptorCompletionInformation");
    DECLARE_CONST_UNICODE_STRING(requestCountName, L"Rc901aDescriptorRequestCount");
    DECLARE_CONST_UNICODE_STRING(ioControlCodeName, L"Rc901aLastIoControlCode");
    DECLARE_CONST_UNICODE_STRING(inputHistoryName, L"Rc901aInputReportHistory");
    DECLARE_CONST_UNICODE_STRING(inputCountName, L"Rc901aInputReportCount");
    DECLARE_CONST_UNICODE_STRING(inputTotalName, L"Rc901aInputReportTotal");
    DECLARE_CONST_UNICODE_STRING(inputStatusName, L"Rc901aInputReportCompletionStatus");
    DECLARE_CONST_UNICODE_STRING(inputInformationName, L"Rc901aInputReportCompletionInformation");

    device = (WDFDEVICE)WdfWorkItemGetParentObject(WorkItem);
    context = Rc901aGetUmdfDeviceContext(device);
    (void)memset(descriptor, 0, sizeof(descriptor));
    (void)memset(inputReports, 0, sizeof(inputReports));

    WdfSpinLockAcquire(context->CaptureLock);
    descriptorLength = context->DescriptorLength;
    if (descriptorLength > sizeof(descriptor)) {
        descriptorLength = sizeof(descriptor);
    }
    if (descriptorLength > 0U) {
        (void)memcpy(descriptor, context->Descriptor, descriptorLength);
    }
    captureStatus = context->CaptureStatus;
    repairStatus = context->RepairStatus;
    lowerStatus = context->LowerStatus;
    lowerInformation = context->LowerInformation;
    requestCount = context->DescriptorRequestCount;
    lastIoControlCode = context->LastIoControlCode;
    inputReportCount = 0U;
    (void)Rc901aCopyInputReportHistory(
        &context->InputReports,
        inputReports,
        sizeof(inputReports) / sizeof(inputReports[0]),
        &inputReportCount
        );
    inputReportTotal = (ULONGLONG)context->InputReports.TotalReports;
    lastInputStatus = context->LastInputStatus;
    lastInputInformation = context->LastInputInformation;
    persistedGeneration = InterlockedCompareExchange(
        &context->CaptureGeneration,
        0,
        0
        );
    WdfSpinLockRelease(context->CaptureLock);

    lowerInformation64 = (ULONGLONG)lowerInformation;
    lastInputInformation64 = (ULONGLONG)lastInputInformation;
    status = WdfDriverOpenParametersRegistryKey(
        WdfDeviceGetDriver(device),
        KEY_READ | KEY_SET_VALUE,
        WDF_NO_OBJECT_ATTRIBUTES,
        &captureKey
        );
    if (NT_SUCCESS(status)) {
        (void)WdfRegistryAssignULong(
            captureKey,
            &captureStatusName,
            (ULONG)captureStatus
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &repairStatusName,
            (ULONG)repairStatus
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &lengthName,
            (ULONG)descriptorLength
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &completionStatusName,
            (ULONG)lowerStatus
            );
        (void)WdfRegistryAssignValue(
            captureKey,
            &completionInformationName,
            REG_QWORD,
            (ULONG)sizeof(lowerInformation64),
            &lowerInformation64
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &requestCountName,
            requestCount
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &ioControlCodeName,
            lastIoControlCode
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &inputCountName,
            (ULONG)inputReportCount
            );
        (void)WdfRegistryAssignValue(
            captureKey,
            &inputTotalName,
            REG_QWORD,
            (ULONG)sizeof(inputReportTotal),
            &inputReportTotal
            );
        (void)WdfRegistryAssignULong(
            captureKey,
            &inputStatusName,
            (ULONG)lastInputStatus
            );
        (void)WdfRegistryAssignValue(
            captureKey,
            &inputInformationName,
            REG_QWORD,
            (ULONG)sizeof(lastInputInformation64),
            &lastInputInformation64
            );
        if (inputReportCount > 0U) {
            (void)WdfRegistryAssignValue(
                captureKey,
                &inputHistoryName,
                REG_BINARY,
                (ULONG)(inputReportCount * sizeof(inputReports[0])),
                inputReports
                );
        }
        else {
            (void)WdfRegistryRemoveValue(captureKey, &inputHistoryName);
        }

        if (captureStatus == Rc901aCaptureSuccess &&
            descriptorLength > 0U &&
            Rc901aComputeSha256(
                descriptor,
                descriptorLength,
                digest
                ) != 0) {
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
        else {
            (void)WdfRegistryRemoveValue(captureKey, &descriptorName);
            (void)WdfRegistryRemoveValue(captureKey, &digestName);
        }

        WdfRegistryClose(captureKey);
    }

    InterlockedExchange(&context->WorkItemQueued, 0);
    if (InterlockedCompareExchange(
            &context->CaptureGeneration,
            0,
            0) != persistedGeneration) {
        Rc901aQueuePersistWorkItem(context);
    }
}
