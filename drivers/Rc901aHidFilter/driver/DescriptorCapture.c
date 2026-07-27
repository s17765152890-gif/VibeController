#include "DescriptorCapture.h"
#include "Sha256.h"

#include <string.h>

#define RC901A_KNOWN_REPORT_DESCRIPTOR_LENGTH 170U

static const unsigned char g_rc901aKnownReportDescriptorSha256[
    RC901A_SHA256_DIGEST_SIZE] = {
    0x2F, 0xEB, 0xD5, 0x02, 0x94, 0xEF, 0x32, 0x7B,
    0xE7, 0x52, 0x3D, 0xE2, 0xF4, 0xB5, 0xCA, 0xCA,
    0xE4, 0x5D, 0x3F, 0x2D, 0xD0, 0x0B, 0x6A, 0x2B,
    0x74, 0x9A, 0x63, 0xA2, 0x66, 0x62, 0xD5, 0xF9
};

static const size_t g_rc901aOpaqueCollectionOffsets[] = {
    0x5AU,
    0x6EU,
    0x82U,
    0x96U
};

static const unsigned char g_rc901aVendorDefinedCollectionTemplate[] = {
    0x06, 0x00, 0xFF,
    0x09, 0x01,
    0xA1, 0x01,
    0x85, 0x00,
    0x95, 0xFF,
    0x75, 0x08,
    0x19, 0x00,
    0x29, 0xFF,
    0x81, 0x00,
    0xC0
};

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

RC901A_DESCRIPTOR_REPAIR_RESULT
Rc901aRepairReportDescriptor(
    unsigned char* descriptor,
    size_t descriptorLength
    )
{
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE];

    if (descriptor == NULL) {
        return Rc901aDescriptorRepairInvalidArgument;
    }

    if (descriptorLength != RC901A_KNOWN_REPORT_DESCRIPTOR_LENGTH) {
        return Rc901aDescriptorRepairNotApplicable;
    }

    if (Rc901aComputeSha256(descriptor, descriptorLength, digest) == 0) {
        return Rc901aDescriptorRepairInvalidArgument;
    }

    if (memcmp(
            digest,
            g_rc901aKnownReportDescriptorSha256,
            sizeof(g_rc901aKnownReportDescriptorSha256)) != 0) {
        return Rc901aDescriptorRepairNotApplicable;
    }

    for (size_t index = 0;
         index < sizeof(g_rc901aOpaqueCollectionOffsets) /
             sizeof(g_rc901aOpaqueCollectionOffsets[0]);
         ++index) {
        const size_t offset = g_rc901aOpaqueCollectionOffsets[index];

        if (offset + sizeof(g_rc901aVendorDefinedCollectionTemplate) >
                descriptorLength ||
            descriptor[offset] != 0x05U ||
            descriptor[offset + 1U] != 0x0CU ||
            descriptor[offset + 6U] != 0x85U ||
            descriptor[offset + 17U] != 0x81U ||
            descriptor[offset + 18U] != 0x00U ||
            descriptor[offset + 19U] != 0xC0U) {
            return Rc901aDescriptorRepairNotApplicable;
        }
    }

    for (size_t index = 0;
         index < sizeof(g_rc901aOpaqueCollectionOffsets) /
             sizeof(g_rc901aOpaqueCollectionOffsets[0]);
         ++index) {
        const size_t offset = g_rc901aOpaqueCollectionOffsets[index];
        const unsigned char reportId = descriptor[offset + 7U];

        (void)memcpy(
            descriptor + offset,
            g_rc901aVendorDefinedCollectionTemplate,
            sizeof(g_rc901aVendorDefinedCollectionTemplate));
        descriptor[offset + 8U] = reportId;
    }

    return Rc901aDescriptorRepairApplied;
}
