/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */

#define OP_ALLOC_IMPLEMENTATION
#include "op_alloc.h"
#include <limits.h>
#include <stddef.h>
#ifdef OP_NBIS_TESTING
#include <stdatomic.h>
#endif
typedef struct allocation {
    struct allocation *previous, *next;
    size_t size;
    max_align_t alignment;
} allocation;
static allocation *head;
jmp_buf op_allocation_failure;
#ifdef OP_NBIS_TESTING
static int allocations_until_failure = -1;
static int allocation_attempts;
/* Detached blocks are released after unlocking. Count them atomically as well
 * as the active list so a missed detached cleanup remains observable. */
static _Atomic int outstanding_allocations;
void op_fail_allocation_after(int count) {
    allocations_until_failure = count;
    allocation_attempts = 0;
}
int op_allocation_attempts(void) { return allocation_attempts; }
int op_live_allocations(void) {
    return atomic_load_explicit(&outstanding_allocations, memory_order_relaxed);
}
#endif
void *op_malloc(size_t size) {
    allocation *a;
#ifdef OP_NBIS_TESTING
    allocation_attempts++;
    if (allocations_until_failure == 0)
        longjmp(op_allocation_failure, 1);
    if (allocations_until_failure > 0)
        allocations_until_failure--;
#endif
    if (size > SIZE_MAX - sizeof(allocation))
        longjmp(op_allocation_failure, 1);
    a = malloc(sizeof(allocation) + size);
    if (!a)
        longjmp(op_allocation_failure, 1);
    a->size = size;
    a->previous = NULL;
    a->next = head;
    if (head)
        head->previous = a;
    head = a;
#ifdef OP_NBIS_TESTING
    atomic_fetch_add_explicit(&outstanding_allocations, 1, memory_order_relaxed);
#endif
    return a + 1;
}
void *op_calloc(size_t count, size_t size) {
    void *p;
    if (size && count > SIZE_MAX / size)
        longjmp(op_allocation_failure, 1);
    p = op_malloc(count * size);
    if (p)
        memset(p, 0, count * size);
    return p;
}
/* A volatile function pointer keeps the wipe from being deleted as a dead store. */
static void *(*volatile secure_memset)(void *, int, size_t) = memset;
static void wipe(void *ptr, size_t size) {
    if (ptr && size)
        secure_memset(ptr, 0, size);
}
static void unlink_allocation(allocation *a) {
    if (a->previous)
        a->previous->next = a->next;
    else
        head = a->next;
    if (a->next)
        a->next->previous = a->previous;
}
void op_free(void *ptr) {
    allocation *a;
    if (!ptr)
        return;
    a = ((allocation *)ptr) - 1;
    unlink_allocation(a);
    wipe(ptr, a->size);
    free(a);
#ifdef OP_NBIS_TESTING
    atomic_fetch_sub_explicit(&outstanding_allocations, 1, memory_order_relaxed);
#endif
}
void *op_detach_allocations(void) {
    allocation *list = head;
    head = NULL;
    return list;
}
void op_release_detached(void *list) {
    allocation *a = list;
    while (a) {
        allocation *next = a->next;
        wipe(a + 1, a->size);
        free(a);
#ifdef OP_NBIS_TESTING
        atomic_fetch_sub_explicit(&outstanding_allocations, 1, memory_order_relaxed);
#endif
        a = next;
    }
}
void *op_realloc(void *ptr, size_t size) {
    void *next;
    allocation *a;
    if (!ptr)
        return op_malloc(size);
    if (!size) {
        op_free(ptr);
        return NULL;
    }
    a = ((allocation *)ptr) - 1;
    next = op_malloc(size);
    if (!next)
        return NULL;
    memcpy(next, ptr, a->size < size ? a->size : size);
    op_free(ptr);
    return next;
}
void op_release_allocations(void) {
    while (head)
        op_free(head + 1);
}

/* Status codes cross the ABI; upstream diagnostics can include minutia values. */
int op_nbis_fprintf(FILE *stream, const char *format, ...) {
    (void)stream;
    (void)format;
    return 0;
}
int op_nbis_printf(const char *format, ...) {
    (void)format;
    return 0;
}
