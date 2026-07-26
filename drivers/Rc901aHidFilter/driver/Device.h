#pragma once

#include <ntddk.h>
#include <wdf.h>

#include "DescriptorCapture.h"

#define RC901A_MAX_OBSERVED_REQUESTS 32U

typedef struct _RC901A_DEVICE_CONTEXT {
    WDFSPINLOCK CaptureLock;
    WDFWORKITEM PersistWorkItem;
    unsigned char Descriptor[RC901A_MAX_REPORT_DESCRIPTOR_SIZE];
    size_t DescriptorLength;
    RC901A_CAPTURE_RESULT CaptureStatus;
    ULONG ObservedRequestCount;
    ULONG ObservedStoredCount;
    ULONG ObservedMajorFunctions[RC901A_MAX_OBSERVED_REQUESTS];
    ULONG ObservedIoControlCodes[RC901A_MAX_OBSERVED_REQUESTS];
    ULONG LastMajorFunction;
    ULONG LastIoControlCode;
    ULONG CompletionCount;
    NTSTATUS LastCompletionStatus;
    ULONG LastCompletionInformation;
    volatile LONG CaptureGeneration;
    volatile LONG WorkItemQueued;
} RC901A_DEVICE_CONTEXT, *PRC901A_DEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(RC901A_DEVICE_CONTEXT, Rc901aGetDeviceContext)

EVT_WDF_DRIVER_DEVICE_ADD Rc901aEvtDeviceAdd;
EVT_WDFDEVICE_WDM_IRP_PREPROCESS Rc901aEvtWdmIrpPreprocess;
EVT_WDF_WORKITEM Rc901aEvtPersistCapture;
