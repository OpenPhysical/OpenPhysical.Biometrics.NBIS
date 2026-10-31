/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */

#ifndef OP_NBIS_H
#define OP_NBIS_H
#include <stdint.h>
#ifdef _WIN32
#define OP_API __declspec(dllexport)
#else
#define OP_API __attribute__((visibility("default")))
#endif
/* ABI v1: all integers are signed 32-bit, pointers remain caller-owned.
 * XYT uses NIST bottom-left pixels: y=height-source_y, angle degrees CCW,
 * pointing away from ridge endings (opposite INCITS 378).
 * quality is 0..100. type is 0=unknown, 1=ending, 2=bifurcation.
 * Calls are serialized inside the library. No handles or allocations escape.
 * Status: 0 success, 1 invalid input, 2 capacity exceeded, 3 engine failure.
 */
typedef struct {
    int32_t x, y, angle, quality, type;
} op_minutia;
OP_API int32_t op_nbis_abi_version(void);
OP_API int32_t op_nbis_extract(const uint8_t *pixels, int32_t length, int32_t width, int32_t height,
                               int32_t dpi, op_minutia *output, int32_t capacity, int32_t *count,
                               int32_t *engine_error);
OP_API int32_t op_nbis_match(const op_minutia *probe, int32_t probe_count,
                             const op_minutia *gallery, int32_t gallery_count, int32_t *score);
#endif
