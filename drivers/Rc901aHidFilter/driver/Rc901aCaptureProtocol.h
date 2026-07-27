#pragma once

#include <guiddef.h>
#include <winioctl.h>

static const GUID GUID_DEVINTERFACE_VIBECONTROLLER_RC901A_CAPTURE = {
    0x34826b0c, 0xf006, 0x44e1,
    { 0xae, 0x98, 0xa5, 0x84, 0xb6, 0x8c, 0x4e, 0xc1 }
};

#define RC901A_CAPTURE_DEVICE_TYPE 0x8010U
#define IOCTL_RC901A_GET_INPUT_REPORTS \
    CTL_CODE( \
        RC901A_CAPTURE_DEVICE_TYPE, \
        0x800U, \
        METHOD_BUFFERED, \
        FILE_READ_ACCESS)
