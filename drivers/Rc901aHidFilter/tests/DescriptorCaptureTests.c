#include "DescriptorCapture.h"
#include "Sha256.h"

#include <stdio.h>
#include <string.h>

static int g_failures = 0;

#define EXPECT_TRUE(condition)                                                     \
    do {                                                                           \
        if (!(condition)) {                                                        \
            (void)fprintf(stderr, "%s:%d expectation failed: %s\n",              \
                __FILE__, __LINE__, #condition);                                   \
            ++g_failures;                                                          \
        }                                                                          \
    } while (0)

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

    if (g_failures != 0) {
        (void)fprintf(stderr, "%d descriptor capture test(s) failed.\n", g_failures);
        return 1;
    }

    (void)printf("All descriptor capture tests passed.\n");
    return 0;
}
