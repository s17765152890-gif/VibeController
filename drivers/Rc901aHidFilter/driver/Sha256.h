#pragma once

#include <stddef.h>

#define RC901A_SHA256_DIGEST_SIZE 32U

int
Rc901aComputeSha256(
    const unsigned char* data,
    size_t dataLength,
    unsigned char digest[RC901A_SHA256_DIGEST_SIZE]
    );
