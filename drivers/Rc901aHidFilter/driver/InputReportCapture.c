#include "InputReportCapture.h"

#include <string.h>

void
Rc901aInitializeInputReportHistory(
    RC901A_INPUT_REPORT_HISTORY* history
    )
{
    if (history != NULL) {
        (void)memset(history, 0, sizeof(*history));
    }
}

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aRecordInputReport(
    RC901A_INPUT_REPORT_HISTORY* history,
    uint32_t ioControlCode,
    const unsigned char* report,
    size_t reportLength
    )
{
    RC901A_INPUT_REPORT_RECORD* record;

    if (history == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    if (reportLength == 0U) {
        return Rc901aInputReportCaptureEmpty;
    }
    if (report == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    if (reportLength > RC901A_MAX_INPUT_REPORT_SIZE) {
        return Rc901aInputReportCaptureTooLarge;
    }

    record = &history->Records[history->NextIndex];
    (void)memset(record, 0, sizeof(*record));
    history->TotalReports += 1U;
    record->Sequence = history->TotalReports;
    record->IoControlCode = ioControlCode;
    record->Length = (uint32_t)reportLength;
    (void)memcpy(record->Data, report, reportLength);

    history->NextIndex =
        (history->NextIndex + 1U) %
        RC901A_INPUT_REPORT_HISTORY_CAPACITY;
    if (history->Count < RC901A_INPUT_REPORT_HISTORY_CAPACITY) {
        history->Count += 1U;
    }

    return Rc901aInputReportCaptureSuccess;
}

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aCopyInputReportHistory(
    const RC901A_INPUT_REPORT_HISTORY* history,
    RC901A_INPUT_REPORT_RECORD* destination,
    size_t destinationCapacity,
    size_t* recordsWritten
    )
{
    size_t sourceIndex;

    if (recordsWritten == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    *recordsWritten = 0U;
    if (history == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    if (history->Count == 0U) {
        return Rc901aInputReportCaptureEmpty;
    }
    if (destination == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    if (destinationCapacity < history->Count) {
        return Rc901aInputReportCaptureDestinationTooSmall;
    }

    sourceIndex =
        history->Count == RC901A_INPUT_REPORT_HISTORY_CAPACITY
        ? history->NextIndex
        : 0U;
    for (size_t index = 0U; index < history->Count; ++index) {
        destination[index] = history->Records[sourceIndex];
        sourceIndex =
            (sourceIndex + 1U) %
            RC901A_INPUT_REPORT_HISTORY_CAPACITY;
    }

    *recordsWritten = history->Count;
    return Rc901aInputReportCaptureSuccess;
}

RC901A_INPUT_REPORT_CAPTURE_RESULT
Rc901aBuildInputReportSnapshot(
    const RC901A_INPUT_REPORT_HISTORY* history,
    RC901A_INPUT_REPORT_SNAPSHOT* destination,
    size_t destinationCapacity,
    size_t* bytesWritten
    )
{
    size_t requiredCapacity;
    size_t recordsWritten;
    RC901A_INPUT_REPORT_CAPTURE_RESULT copyResult;

    if (bytesWritten == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }
    *bytesWritten = 0U;
    if (history == NULL || destination == NULL) {
        return Rc901aInputReportCaptureInvalidArgument;
    }

    requiredCapacity =
        RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE +
        history->Count * sizeof(RC901A_INPUT_REPORT_RECORD);
    if (destinationCapacity < requiredCapacity) {
        return Rc901aInputReportCaptureDestinationTooSmall;
    }

    (void)memset(destination, 0, requiredCapacity);
    destination->Version = RC901A_CAPTURE_PROTOCOL_VERSION;
    destination->RecordSize = RC901A_INPUT_REPORT_RECORD_SIZE;
    destination->TotalReports = history->TotalReports;
    destination->RecordCount = (uint32_t)history->Count;

    if (history->Count > 0U) {
        recordsWritten = 0U;
        copyResult = Rc901aCopyInputReportHistory(
            history,
            destination->Records,
            RC901A_INPUT_REPORT_HISTORY_CAPACITY,
            &recordsWritten
            );
        if (copyResult != Rc901aInputReportCaptureSuccess ||
            recordsWritten != history->Count) {
            (void)memset(destination, 0, requiredCapacity);
            return copyResult;
        }
    }

    *bytesWritten = requiredCapacity;
    return Rc901aInputReportCaptureSuccess;
}
