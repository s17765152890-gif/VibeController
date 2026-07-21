#include "DescriptorCapture.h"

#include <string.h>

RC901A_CAPTURE_RESULT
Rc901aCopyDescriptor(
    const unsigned char* source,
    size_t sourceLength,
    unsigned char* destination,
    size_t destinationCapacity,
    size_t* bytesWritten
    )
{
    if (bytesWritten == NULL) {
        return Rc901aCaptureInvalidArgument;
    }

    *bytesWritten = 0;

    if (sourceLength == 0U) {
        return Rc901aCaptureEmpty;
    }

    if (source == NULL || destination == NULL) {
        return Rc901aCaptureInvalidArgument;
    }

    if (sourceLength > RC901A_MAX_REPORT_DESCRIPTOR_SIZE) {
        return Rc901aCaptureTooLarge;
    }

    if (destinationCapacity < sourceLength) {
        return Rc901aCaptureDestinationTooSmall;
    }

    (void)memcpy(destination, source, sourceLength);
    *bytesWritten = sourceLength;
    return Rc901aCaptureSuccess;
}
