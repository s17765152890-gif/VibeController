#pragma once

#include <stddef.h>
#include <stdint.h>

#define RC901A_MAX_INPUT_REPORT_SIZE 256U
#define RC901A_INPUT_REPORT_HISTORY_CAPACITY 32U
#define RC901A_CAPTURE_PROTOCOL_VERSION 1U
#define RC901A_INPUT_REPORT_RECORD_SIZE 272U
#define RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE 24U

typedef enum _RC901A_INPUT_REPORT_CAPTURE_RESULT {
    Rc901aInputReportCaptureSuccess = 0,
    Rc901aInputReportCaptureEmpty = 1,
    Rc901aInputReportCaptureInvalidArgument = 2,
    Rc901aInputReportCaptureTooLarge = 3,
    Rc901aInputReportCaptureDestinationTooSmall = 4
} RC901A_INPUT_REPORT_CAPTURE_RESULT;

typedef struct _RC901A_INPUT_REPORT_RECORD {
    uint64_t Sequence;
    uint32_t IoControlCode;
    uint32_t Length;
    unsigned char Data[RC901A_MAX_INPUT_REPORT_SIZE];
} RC901A_INPUT_REPORT_RECORD;

typedef struct _RC901A_INPUT_REPORT_HISTORY {
    uint64_t TotalReports;
    size_t Count;
    size_t NextIndex;
    RC901A_INPUT_REPORT_RECORD Records[
        RC901A_INPUT_REPORT_HISTORY_CAPACITY];
} RC901A_INPUT_REPORT_HISTORY;

typedef struct _RC901A_INPUT_REPORT_SNAPSHOT {
    uint32_t Version;
    uint32_t RecordSize;
    uint64_t TotalReports;
    uint32_t RecordCount;
    uint32_t Reserved;
    RC901A_INPUT_REPORT_RECORD Records[
        RC901A_INPUT_REPORT_HISTORY_CAPACITY];
} RC901A_INPUT_REPORT_SNAPSHOT;

void
Rc901aInitializeInputReportHistory(
    RC901A_INPUT_REPORT_HISTORY* history
    );

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aRecordInputReport(
    RC901A_INPUT_REPORT_HISTORY* history,
    uint32_t ioControlCode,
    const unsigned char* report,
    size_t reportLength
    );

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aCopyInputReportHistory(
    const RC901A_INPUT_REPORT_HISTORY* history,
    RC901A_INPUT_REPORT_RECORD* destination,
    size_t destinationCapacity,
    size_t* recordsWritten
    );

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aBuildInputReportSnapshot(
    const RC901A_INPUT_REPORT_HISTORY* history,
    RC901A_INPUT_REPORT_SNAPSHOT* destination,
    size_t destinationCapacity,
    size_t* bytesWritten
    );
