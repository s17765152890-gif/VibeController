#include "Sha256.h"

#include <stdint.h>
#include <string.h>

#define RC901A_SHA256_BLOCK_SIZE 64U

typedef struct RC901A_SHA256_CONTEXT {
    unsigned char block[RC901A_SHA256_BLOCK_SIZE];
    size_t blockLength;
    uint64_t totalLength;
    uint32_t state[8];
} RC901A_SHA256_CONTEXT;

static const uint32_t g_roundConstants[64] = {
    0x428A2F98U, 0x71374491U, 0xB5C0FBCFU, 0xE9B5DBA5U,
    0x3956C25BU, 0x59F111F1U, 0x923F82A4U, 0xAB1C5ED5U,
    0xD807AA98U, 0x12835B01U, 0x243185BEU, 0x550C7DC3U,
    0x72BE5D74U, 0x80DEB1FEU, 0x9BDC06A7U, 0xC19BF174U,
    0xE49B69C1U, 0xEFBE4786U, 0x0FC19DC6U, 0x240CA1CCU,
    0x2DE92C6FU, 0x4A7484AAU, 0x5CB0A9DCU, 0x76F988DAU,
    0x983E5152U, 0xA831C66DU, 0xB00327C8U, 0xBF597FC7U,
    0xC6E00BF3U, 0xD5A79147U, 0x06CA6351U, 0x14292967U,
    0x27B70A85U, 0x2E1B2138U, 0x4D2C6DFCU, 0x53380D13U,
    0x650A7354U, 0x766A0ABBU, 0x81C2C92EU, 0x92722C85U,
    0xA2BFE8A1U, 0xA81A664BU, 0xC24B8B70U, 0xC76C51A3U,
    0xD192E819U, 0xD6990624U, 0xF40E3585U, 0x106AA070U,
    0x19A4C116U, 0x1E376C08U, 0x2748774CU, 0x34B0BCB5U,
    0x391C0CB3U, 0x4ED8AA4AU, 0x5B9CCA4FU, 0x682E6FF3U,
    0x748F82EEU, 0x78A5636FU, 0x84C87814U, 0x8CC70208U,
    0x90BEFFFAU, 0xA4506CEBU, 0xBEF9A3F7U, 0xC67178F2U
};

static uint32_t RotateRight(uint32_t value, unsigned int count)
{
    return (value >> count) | (value << (32U - count));
}

static void Transform(RC901A_SHA256_CONTEXT* context)
{
    uint32_t words[64];

    for (size_t index = 0; index < 16U; ++index) {
        const size_t offset = index * 4U;
        words[index] =
            ((uint32_t)context->block[offset] << 24U) |
            ((uint32_t)context->block[offset + 1U] << 16U) |
            ((uint32_t)context->block[offset + 2U] << 8U) |
            (uint32_t)context->block[offset + 3U];
    }

    for (size_t index = 16U; index < 64U; ++index) {
        const uint32_t first =
            RotateRight(words[index - 15U], 7U) ^
            RotateRight(words[index - 15U], 18U) ^
            (words[index - 15U] >> 3U);
        const uint32_t second =
            RotateRight(words[index - 2U], 17U) ^
            RotateRight(words[index - 2U], 19U) ^
            (words[index - 2U] >> 10U);
        words[index] = words[index - 16U] + first + words[index - 7U] + second;
    }

    uint32_t a = context->state[0];
    uint32_t b = context->state[1];
    uint32_t c = context->state[2];
    uint32_t d = context->state[3];
    uint32_t e = context->state[4];
    uint32_t f = context->state[5];
    uint32_t g = context->state[6];
    uint32_t h = context->state[7];

    for (size_t index = 0; index < 64U; ++index) {
        const uint32_t sum1 =
            RotateRight(e, 6U) ^ RotateRight(e, 11U) ^ RotateRight(e, 25U);
        const uint32_t choose = (e & f) ^ ((~e) & g);
        const uint32_t temporary1 = h + sum1 + choose + g_roundConstants[index] + words[index];
        const uint32_t sum0 =
            RotateRight(a, 2U) ^ RotateRight(a, 13U) ^ RotateRight(a, 22U);
        const uint32_t majority = (a & b) ^ (a & c) ^ (b & c);
        const uint32_t temporary2 = sum0 + majority;

        h = g;
        g = f;
        f = e;
        e = d + temporary1;
        d = c;
        c = b;
        b = a;
        a = temporary1 + temporary2;
    }

    context->state[0] += a;
    context->state[1] += b;
    context->state[2] += c;
    context->state[3] += d;
    context->state[4] += e;
    context->state[5] += f;
    context->state[6] += g;
    context->state[7] += h;
}

static void Initialize(RC901A_SHA256_CONTEXT* context)
{
    (void)memset(context, 0, sizeof(*context));
    context->state[0] = 0x6A09E667U;
    context->state[1] = 0xBB67AE85U;
    context->state[2] = 0x3C6EF372U;
    context->state[3] = 0xA54FF53AU;
    context->state[4] = 0x510E527FU;
    context->state[5] = 0x9B05688CU;
    context->state[6] = 0x1F83D9ABU;
    context->state[7] = 0x5BE0CD19U;
}

static void Update(
    RC901A_SHA256_CONTEXT* context,
    const unsigned char* data,
    size_t dataLength
    )
{
    context->totalLength += (uint64_t)dataLength;

    for (size_t index = 0; index < dataLength; ++index) {
        context->block[context->blockLength++] = data[index];
        if (context->blockLength == RC901A_SHA256_BLOCK_SIZE) {
            Transform(context);
            context->blockLength = 0;
        }
    }
}

static void Finalize(
    RC901A_SHA256_CONTEXT* context,
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE]
    )
{
    size_t index = context->blockLength;
    context->block[index++] = 0x80U;

    if (index > 56U) {
        (void)memset(context->block + index, 0, RC901A_SHA256_BLOCK_SIZE - index);
        Transform(context);
        index = 0;
    }

    (void)memset(context->block + index, 0, 56U - index);

    const uint64_t bitLength = context->totalLength * 8U;
    for (size_t byteIndex = 0; byteIndex < 8U; ++byteIndex) {
        context->block[63U - byteIndex] =
            (unsigned char)(bitLength >> (byteIndex * 8U));
    }

    Transform(context);

    for (size_t wordIndex = 0; wordIndex < 8U; ++wordIndex) {
        digest[wordIndex * 4U] = (unsigned char)(context->state[wordIndex] >> 24U);
        digest[wordIndex * 4U + 1U] = (unsigned char)(context->state[wordIndex] >> 16U);
        digest[wordIndex * 4U + 2U] = (unsigned char)(context->state[wordIndex] >> 8U);
        digest[wordIndex * 4U + 3U] = (unsigned char)context->state[wordIndex];
    }
}

int
Rc901aComputeSha256(
    const unsigned char* data,
    size_t dataLength,
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE]
    )
{
    if (digest == NULL || (data == NULL && dataLength != 0U)) {
        return 0;
    }

    RC901A_SHA256_CONTEXT context;
    Initialize(&context);
    if (dataLength != 0U) {
        Update(&context, data, dataLength);
    }
    Finalize(&context, digest);
    return 1;
}
