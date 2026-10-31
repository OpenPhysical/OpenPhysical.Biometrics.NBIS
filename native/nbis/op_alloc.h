/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */

#ifndef OP_ALLOC_H
#define OP_ALLOC_H
#if defined(LOG_REPORT) || defined(TIMER) || defined(DEBUG)
#error NBIS diagnostic and timer modes must remain disabled in the wrapper
#endif
#include <setjmp.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
/* NBIS never retains heap objects across calls. All allocations belong to the
 * serialized call and are reclaimed even when a deep upstream error leaks them.
 */
int op_nbis_fprintf(FILE *stream, const char *format, ...);
int op_nbis_printf(const char *format, ...);
extern jmp_buf op_allocation_failure;
void *op_malloc(size_t size);
void *op_calloc(size_t count, size_t size);
void *op_realloc(void *ptr, size_t size);
void op_free(void *ptr);
void op_release_allocations(void);
/* Move the call's allocations off the process-wide list so they can be wiped
 * after the engine mutex is released. */
void *op_detach_allocations(void);
void op_release_detached(void *list);
#ifdef OP_NBIS_TESTING
void op_fail_allocation_after(int count);
int op_allocation_attempts(void);
int op_live_allocations(void);
#endif
#ifndef OP_ALLOC_IMPLEMENTATION
#define fprintf op_nbis_fprintf
#define printf op_nbis_printf
#define malloc op_malloc
#define calloc op_calloc
#define realloc op_realloc
#define free op_free
#define exit(code) longjmp(op_allocation_failure, 1)
#endif
#endif
