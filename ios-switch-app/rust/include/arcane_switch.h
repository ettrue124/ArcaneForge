#pragma once
#include <stddef.h>
#include <stdint.h>

#define ARCANE_SWITCH_STATUS 0
#define ARCANE_SWITCH_PRIVATE 1
#define ARCANE_SWITCH_OFFICIAL 2

// Returns 0 on success, 1 on failure; `out` receives a NUL-terminated message.
int32_t arcane_switch_run(int32_t action, const uint8_t *pairing, size_t pairing_len,
                          const char *device_ip, const char *server_host,
                          char *out, size_t out_capacity);
