#pragma once

#include <stddef.h>

#define RC901A_MAX_REPORT_DESCRIPTOR_SIZE 4096U

typedef enum RC901A_CAPTURE_RESULT {
    Rc901aCaptureSuccess = 0,
    Rc901aCaptureInvalidArgument,
    Rc901aCaptureEmpty,
    Rc901aCaptureTooLarge,
    Rc901aCaptureDestinationTooSmall
} RC901A_CAPTURE_RESULT;

RC901A_CAPTURE_RESULT
Rc901aCopyDescriptor(
    const unsigned char* source,
    size_t sourceLength,
    unsigned char* destination,
    size_t destinationCapacity,
    size_t* bytesWritten
    );
