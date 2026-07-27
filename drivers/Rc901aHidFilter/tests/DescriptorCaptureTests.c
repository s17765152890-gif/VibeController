#include "DescriptorCapture.h"
#include "InputReportCapture.h"
#include "Sha256.h"

#include <stdio.h>
#include <string.h>

_Static_assert(
    sizeof(RC901A_INPUT_REPORT_RECORD) == RC901A_INPUT_REPORT_RECORD_SIZE,
    "RC901A input report wire record must remain 272 bytes");

static int g_failures = 0;

#define EXPECT_TRUE(condition)                                                     \
    do {                                                                           \
        if (!(condition)) {                                                        \
            (void)fprintf(stderr, "%s:%d expectation failed: %s\n",              \
                __FILE__, __LINE__, #condition);                                   \
            ++g_failures;                                                          \
        }                                                                          \
    } while (0)

static const unsigned char g_rc901aCapturedReportDescriptor[] = {
    0x05, 0x01, 0x09, 0x06, 0xA1, 0x01, 0x85, 0x01,
    0x05, 0x07, 0x19, 0xE0, 0x29, 0xE7, 0x15, 0x00,
    0x25, 0x01, 0x75, 0x01, 0x95, 0x08, 0x81, 0x02,
    0x95, 0x01, 0x75, 0x08, 0x81, 0x01, 0x95, 0x05,
    0x75, 0x01, 0x05, 0x08, 0x19, 0x01, 0x29, 0x05,
    0x91, 0x02, 0x95, 0x01, 0x75, 0x03, 0x91, 0x01,
    0x95, 0x06, 0x75, 0x08, 0x15, 0x00, 0x25, 0xFF,
    0x05, 0x07, 0x19, 0x00, 0x29, 0xFF, 0x81, 0x00,
    0xC0, 0x05, 0x0C, 0x09, 0x01, 0xA1, 0x01, 0x85,
    0x03, 0x19, 0x00, 0x2A, 0x9C, 0x02, 0x15, 0x00,
    0x26, 0x9C, 0x02, 0x95, 0x01, 0x75, 0x10, 0x81,
    0x00, 0xC0, 0x05, 0x0C, 0x09, 0x01, 0xA1, 0x01,
    0x85, 0xEC, 0x95, 0xFF, 0x75, 0x08, 0x15, 0x00,
    0x26, 0xFF, 0x00, 0x81, 0x00, 0xC0, 0x05, 0x0C,
    0x09, 0x01, 0xA1, 0x01, 0x85, 0xEB, 0x95, 0xFF,
    0x75, 0x08, 0x15, 0x00, 0x26, 0xFF, 0x00, 0x81,
    0x00, 0xC0, 0x05, 0x0C, 0x09, 0x01, 0xA1, 0x01,
    0x85, 0xE9, 0x95, 0xFF, 0x75, 0x08, 0x15, 0x00,
    0x26, 0xFF, 0x00, 0x81, 0x00, 0xC0, 0x05, 0x0C,
    0x09, 0x01, 0xA1, 0x01, 0x85, 0xE8, 0x95, 0xFF,
    0x75, 0x08, 0x15, 0x00, 0x26, 0xFF, 0x00, 0x81,
    0x00, 0xC0
};

static void TestCopiesNormalDescriptor(void)
{
    const unsigned char source[] = { 0x05, 0x0C, 0x09, 0x01, 0x81, 0x02 };
    unsigned char destination[sizeof(source)] = { 0 };
    size_t bytesWritten = 99;

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        source,
        sizeof(source),
        destination,
        sizeof(destination),
        &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureSuccess);
    EXPECT_TRUE(bytesWritten == sizeof(source));
    EXPECT_TRUE(memcmp(source, destination, sizeof(source)) == 0);
}

static void TestRejectsZeroLength(void)
{
    const unsigned char source = 0x05;
    unsigned char destination = 0xAA;
    size_t bytesWritten = 99;

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        &source, 0, &destination, 1, &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureEmpty);
    EXPECT_TRUE(bytesWritten == 0);
    EXPECT_TRUE(destination == 0xAA);
}

static void TestCopiesMaximumDescriptor(void)
{
    static unsigned char source[RC901A_MAX_REPORT_DESCRIPTOR_SIZE];
    static unsigned char destination[RC901A_MAX_REPORT_DESCRIPTOR_SIZE];
    size_t bytesWritten = 0;

    for (size_t index = 0; index < sizeof(source); ++index) {
        source[index] = (unsigned char)(index & 0xFFU);
    }

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        source,
        sizeof(source),
        destination,
        sizeof(destination),
        &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureSuccess);
    EXPECT_TRUE(bytesWritten == sizeof(source));
    EXPECT_TRUE(memcmp(source, destination, sizeof(source)) == 0);
}

static void TestRejectsOversizedDescriptor(void)
{
    const unsigned char source = 0x05;
    unsigned char destination = 0xAA;
    size_t bytesWritten = 99;

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        &source,
        RC901A_MAX_REPORT_DESCRIPTOR_SIZE + 1U,
        &destination,
        1,
        &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureTooLarge);
    EXPECT_TRUE(bytesWritten == 0);
    EXPECT_TRUE(destination == 0xAA);
}

static void TestRejectsNullSource(void)
{
    unsigned char destination = 0xAA;
    size_t bytesWritten = 99;

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        NULL, 1, &destination, 1, &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureInvalidArgument);
    EXPECT_TRUE(bytesWritten == 0);
    EXPECT_TRUE(destination == 0xAA);
}

static void TestRejectsSmallDestination(void)
{
    const unsigned char source[] = { 0x05, 0x0C };
    unsigned char destination = 0xAA;
    size_t bytesWritten = 99;

    const RC901A_CAPTURE_RESULT result = Rc901aCopyDescriptor(
        source, sizeof(source), &destination, 1, &bytesWritten);

    EXPECT_TRUE(result == Rc901aCaptureDestinationTooSmall);
    EXPECT_TRUE(bytesWritten == 0);
    EXPECT_TRUE(destination == 0xAA);
}

static unsigned char HexNibble(char value)
{
    if (value >= '0' && value <= '9') {
        return (unsigned char)(value - '0');
    }

    if (value >= 'A' && value <= 'F') {
        return (unsigned char)(value - 'A' + 10);
    }

    return (unsigned char)(value - 'a' + 10);
}

static void ExpectDigest(const unsigned char* data, size_t length, const char* expectedHex)
{
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE] = { 0 };
    unsigned char expected[RC901A_SHA256_DIGEST_SIZE] = { 0 };

    for (size_t index = 0; index < RC901A_SHA256_DIGEST_SIZE; ++index) {
        expected[index] = (unsigned char)(
            (HexNibble(expectedHex[index * 2U]) << 4U) |
            HexNibble(expectedHex[index * 2U + 1U]));
    }

    EXPECT_TRUE(Rc901aComputeSha256(data, length, digest) != 0);
    EXPECT_TRUE(memcmp(digest, expected, sizeof(digest)) == 0);
}

static void TestSha256KnownVectors(void)
{
    static const unsigned char abc[] = { 'a', 'b', 'c' };

    ExpectDigest(
        NULL,
        0,
        "E3B0C44298FC1C149AFBF4C8996FB924"
        "27AE41E4649B934CA495991B7852B855");
    ExpectDigest(
        abc,
        sizeof(abc),
        "BA7816BF8F01CFEA414140DE5DAE2223"
        "B00361A396177A9CB410FF61F20015AD");
}

static void TestSha256RejectsNullDataWithNonzeroLength(void)
{
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE] = { 0 };

    EXPECT_TRUE(Rc901aComputeSha256(NULL, 1, digest) == 0);
}

static void TestRepairsOpaqueReportsAsVendorDefinedCollections(void)
{
    static const size_t collectionOffsets[] = { 0x5AU, 0x6EU, 0x82U, 0x96U };
    static const unsigned char expectedCollectionTemplate[] = {
        0x06, 0x00, 0xFF,  // Usage Page (Vendor-defined 0xFF00)
        0x09, 0x01,        // Usage (1)
        0xA1, 0x01,        // Collection (Application)
        0x85, 0x00,        // Report ID (filled from the captured descriptor)
        0x95, 0xFF,        // Report Count (255)
        0x75, 0x08,        // Report Size (8)
        0x19, 0x00,        // Usage Minimum (0)
        0x29, 0xFF,        // Usage Maximum (255)
        0x81, 0x00,        // Input (Data, Array, Absolute)
        0xC0               // End Collection
    };
    unsigned char descriptor[sizeof(g_rc901aCapturedReportDescriptor)] = { 0 };
    unsigned char original[sizeof(g_rc901aCapturedReportDescriptor)] = { 0 };

    (void)memcpy(
        descriptor,
        g_rc901aCapturedReportDescriptor,
        sizeof(descriptor));
    (void)memcpy(original, descriptor, sizeof(original));

    EXPECT_TRUE(
        Rc901aRepairReportDescriptor(descriptor, sizeof(descriptor)) ==
        Rc901aDescriptorRepairApplied);

    for (size_t collectionIndex = 0;
         collectionIndex <
             sizeof(collectionOffsets) / sizeof(collectionOffsets[0]);
         ++collectionIndex) {
        unsigned char expectedCollection[sizeof(expectedCollectionTemplate)] = { 0 };
        const size_t offset = collectionOffsets[collectionIndex];

        (void)memcpy(
            expectedCollection,
            expectedCollectionTemplate,
            sizeof(expectedCollection));
        expectedCollection[8] = original[offset + 7U];
        EXPECT_TRUE(
            memcmp(
                descriptor + offset,
                expectedCollection,
                sizeof(expectedCollection)) == 0);

        for (size_t index = 0; index < offset; ++index) {
            int belongsToEarlierOpaqueCollection = 0;

            for (size_t earlierIndex = 0;
                 earlierIndex < collectionIndex;
                 ++earlierIndex) {
                const size_t earlierOffset = collectionOffsets[earlierIndex];
                if (index >= earlierOffset &&
                    index < earlierOffset + sizeof(expectedCollection)) {
                    belongsToEarlierOpaqueCollection = 1;
                    break;
                }
            }

            if (belongsToEarlierOpaqueCollection == 0) {
                EXPECT_TRUE(descriptor[index] == original[index]);
            }
        }
    }
}

static void TestDoesNotRepairUnknownFirmwareDescriptor(void)
{
    unsigned char descriptor[sizeof(g_rc901aCapturedReportDescriptor)] = { 0 };
    unsigned char original[sizeof(g_rc901aCapturedReportDescriptor)] = { 0 };

    (void)memcpy(
        descriptor,
        g_rc901aCapturedReportDescriptor,
        sizeof(descriptor));
    descriptor[0x5FU] ^= 0x01U;
    (void)memcpy(original, descriptor, sizeof(original));

    EXPECT_TRUE(
        Rc901aRepairReportDescriptor(descriptor, sizeof(descriptor)) ==
        Rc901aDescriptorRepairNotApplicable);
    EXPECT_TRUE(memcmp(descriptor, original, sizeof(descriptor)) == 0);
}

static void TestRejectsNullDescriptorRepairBuffer(void)
{
    EXPECT_TRUE(
        Rc901aRepairReportDescriptor(
            NULL,
            sizeof(g_rc901aCapturedReportDescriptor)) ==
        Rc901aDescriptorRepairInvalidArgument);
}

static void TestRecordsAndCopiesInputReportsChronologically(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_RECORD records[2];
    const unsigned char firstReport[] = { 0x03U, 0x24U, 0x00U };
    const unsigned char secondReport[] = { 0x03U, 0x00U, 0x00U };
    size_t recordsWritten = 99U;

    Rc901aInitializeInputReportHistory(&history);
    (void)memset(records, 0xAA, sizeof(records));

    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0x000B0003U,
            firstReport,
            sizeof(firstReport)) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0x000B0003U,
            secondReport,
            sizeof(secondReport)) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        Rc901aCopyInputReportHistory(
            &history,
            records,
            sizeof(records) / sizeof(records[0]),
            &recordsWritten) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(recordsWritten == 2U);
    EXPECT_TRUE(records[0].Sequence == 1U);
    EXPECT_TRUE(records[0].IoControlCode == 0x000B0003U);
    EXPECT_TRUE(records[0].Length == sizeof(firstReport));
    EXPECT_TRUE(
        memcmp(records[0].Data, firstReport, sizeof(firstReport)) == 0);
    EXPECT_TRUE(records[1].Sequence == 2U);
    EXPECT_TRUE(records[1].Length == sizeof(secondReport));
    EXPECT_TRUE(
        memcmp(records[1].Data, secondReport, sizeof(secondReport)) == 0);
}

static void TestInputReportHistoryRetainsNewestReportsAfterWrap(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_RECORD records[
        RC901A_INPUT_REPORT_HISTORY_CAPACITY];
    size_t recordsWritten = 0U;

    Rc901aInitializeInputReportHistory(&history);
    for (size_t index = 0U;
         index < RC901A_INPUT_REPORT_HISTORY_CAPACITY + 2U;
         ++index) {
        const unsigned char report[] = { (unsigned char)(index + 1U) };

        EXPECT_TRUE(
            Rc901aRecordInputReport(
                &history,
                0x000B0003U,
                report,
                sizeof(report)) == Rc901aInputReportCaptureSuccess);
    }

    EXPECT_TRUE(
        Rc901aCopyInputReportHistory(
            &history,
            records,
            sizeof(records) / sizeof(records[0]),
            &recordsWritten) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        recordsWritten == RC901A_INPUT_REPORT_HISTORY_CAPACITY);
    EXPECT_TRUE(records[0].Sequence == 3U);
    EXPECT_TRUE(records[0].Data[0] == 3U);
    EXPECT_TRUE(
        records[recordsWritten - 1U].Sequence ==
        RC901A_INPUT_REPORT_HISTORY_CAPACITY + 2U);
    EXPECT_TRUE(
        records[recordsWritten - 1U].Data[0] ==
        RC901A_INPUT_REPORT_HISTORY_CAPACITY + 2U);
}

static void TestRejectsInvalidInputReports(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    unsigned char oversized[RC901A_MAX_INPUT_REPORT_SIZE + 1U] = { 0 };
    const unsigned char report = 0x03U;
    RC901A_INPUT_REPORT_RECORD record;
    size_t recordsWritten = 99U;

    Rc901aInitializeInputReportHistory(&history);
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0U,
            NULL,
            1U) == Rc901aInputReportCaptureInvalidArgument);
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0U,
            &report,
            0U) == Rc901aInputReportCaptureEmpty);
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0U,
            oversized,
            sizeof(oversized)) == Rc901aInputReportCaptureTooLarge);
    EXPECT_TRUE(
        Rc901aCopyInputReportHistory(
            &history,
            &record,
            1U,
            &recordsWritten) == Rc901aInputReportCaptureEmpty);
    EXPECT_TRUE(recordsWritten == 0U);
}

static void TestBuildsStableInputReportSnapshot(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_SNAPSHOT snapshot;
    const unsigned char press[] = {
        0x01U, 0x00U, 0x00U, 0xF1U, 0x00U,
        0x00U, 0x00U, 0x00U, 0x00U
    };
    size_t bytesWritten = 0U;

    Rc901aInitializeInputReportHistory(&history);
    (void)memset(&snapshot, 0xAA, sizeof(snapshot));
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0U,
            press,
            sizeof(press)) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        Rc901aBuildInputReportSnapshot(
            &history,
            &snapshot,
            sizeof(snapshot),
            &bytesWritten) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(snapshot.Version == RC901A_CAPTURE_PROTOCOL_VERSION);
    EXPECT_TRUE(
        snapshot.RecordSize == RC901A_INPUT_REPORT_RECORD_SIZE);
    EXPECT_TRUE(snapshot.TotalReports == 1U);
    EXPECT_TRUE(snapshot.RecordCount == 1U);
    EXPECT_TRUE(snapshot.Reserved == 0U);
    EXPECT_TRUE(snapshot.Records[0].Sequence == 1U);
    EXPECT_TRUE(snapshot.Records[0].Data[3] == 0xF1U);
    EXPECT_TRUE(
        bytesWritten ==
        RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE +
            sizeof(RC901A_INPUT_REPORT_RECORD));
}

static void TestSnapshotRejectsUndersizedOutput(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_SNAPSHOT snapshot;
    const unsigned char report[] = { 0x01U };
    size_t bytesWritten = 99U;

    Rc901aInitializeInputReportHistory(&history);
    EXPECT_TRUE(
        Rc901aRecordInputReport(
            &history,
            0U,
            report,
            sizeof(report)) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        Rc901aBuildInputReportSnapshot(
            &history,
            &snapshot,
            RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE,
            &bytesWritten) ==
        Rc901aInputReportCaptureDestinationTooSmall);
    EXPECT_TRUE(bytesWritten == 0U);
}

static void TestBuildsEmptySnapshotHeader(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_SNAPSHOT snapshot;
    size_t bytesWritten = 0U;

    Rc901aInitializeInputReportHistory(&history);
    EXPECT_TRUE(
        Rc901aBuildInputReportSnapshot(
            &history,
            &snapshot,
            sizeof(snapshot),
            &bytesWritten) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(snapshot.Version == RC901A_CAPTURE_PROTOCOL_VERSION);
    EXPECT_TRUE(snapshot.TotalReports == 0U);
    EXPECT_TRUE(snapshot.RecordCount == 0U);
    EXPECT_TRUE(
        bytesWritten == RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE);
}

int main(void)
{
    TestCopiesNormalDescriptor();
    TestRejectsZeroLength();
    TestCopiesMaximumDescriptor();
    TestRejectsOversizedDescriptor();
    TestRejectsNullSource();
    TestRejectsSmallDestination();
    TestSha256KnownVectors();
    TestSha256RejectsNullDataWithNonzeroLength();
    TestRepairsOpaqueReportsAsVendorDefinedCollections();
    TestDoesNotRepairUnknownFirmwareDescriptor();
    TestRejectsNullDescriptorRepairBuffer();
    TestRecordsAndCopiesInputReportsChronologically();
    TestInputReportHistoryRetainsNewestReportsAfterWrap();
    TestRejectsInvalidInputReports();
    TestBuildsStableInputReportSnapshot();
    TestSnapshotRejectsUndersizedOutput();
    TestBuildsEmptySnapshotHeader();

    if (g_failures != 0) {
        (void)fprintf(stderr, "%d descriptor capture test(s) failed.\n", g_failures);
        return 1;
    }

    (void)printf("All descriptor capture tests passed.\n");
    return 0;
}
