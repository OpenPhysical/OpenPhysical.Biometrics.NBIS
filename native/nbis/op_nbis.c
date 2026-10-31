/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */

#include "op_nbis.h"
#include "op_alloc.h"
#include <bozorth.h>
#include <lfs.h>
#include <string.h>
#ifdef _WIN32
#include <windows.h>
static SRWLOCK mutex = SRWLOCK_INIT;
#define LOCK() AcquireSRWLockExclusive(&mutex)
#define UNLOCK() ReleaseSRWLockExclusive(&mutex)
#else
#include <pthread.h>
static pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
#define LOCK() pthread_mutex_lock(&mutex)
#define UNLOCK() pthread_mutex_unlock(&mutex)
#endif
int m1_xyt = 0;
int max_minutiae = DEFAULT_BOZORTH_MINUTIAE;
int min_computable_minutiae = MIN_COMPUTABLE_BOZORTH_MINUTIAE;
int verbose_bozorth = 0;
FILE *errorfp = NULL;
int malloc_int_ret(int **ptr, const int n, char *context) {
    (void)context;
    *ptr = malloc((size_t)n * sizeof(int));
    return *ptr ? 0 : -2;
}
int realloc_int_ret(int **ptr, const int n, char *context) {
    int *next;
    (void)context;
    next = realloc(*ptr, (size_t)n * sizeof(int));
    if (!next)
        return -2;
    *ptr = next;
    return 0;
}
/* Engine diagnostics never receive filenames or biometric values. */
char *get_progname(void) { return "OpenPhysical.Biometrics"; }
char *get_probe_filename(void) { return "probe"; }
char *get_gallery_filename(void) { return "gallery"; }
int32_t op_nbis_abi_version(void) { return 1; }
static void finish_locked(void) {
    void *pending = op_detach_allocations();
    UNLOCK();
    op_release_detached(pending);
}
int32_t op_nbis_extract(const uint8_t *pixels, int32_t length, int32_t width, int32_t height,
                        int32_t dpi, op_minutia *output, int32_t capacity, int32_t *count,
                        int32_t *engine_error) {
    MINUTIAE *minutiae = NULL;
    int *q = NULL, *d = NULL, *c = NULL, *f = NULL, *h = NULL;
    unsigned char *binary = NULL, *copy = NULL;
    int mw = 0, mh = 0, bw = 0, bh = 0, bd = 0, ret, i;
    if (!count || !engine_error)
        return 1;
    *count = 0;
    *engine_error = 0;
    if (!pixels || !output || width < 64 || height < 64 || width > 2048 || height > 2048 ||
        length != width * height || dpi != 500 || capacity < 1 || capacity > 1000)
        return 1;
    LOCK();
    /* NBIS contains unchecked allocations. Unwind only C frames to this boundary;
     * the arena owns every engine allocation, including partially built objects. */
    if (setjmp(op_allocation_failure)) {
        *engine_error = -10000;
        finish_locked();
        return 3;
    }
    copy = malloc((size_t)length);
    if (!copy) {
        *engine_error = -1;
        finish_locked();
        return 3;
    }
    memcpy(copy, pixels, (size_t)length);
    ret = get_minutiae(&minutiae, &q, &d, &c, &f, &h, &mw, &mh, &binary, &bw, &bh, &bd, copy, width,
                       height, 8, dpi / 25.4, &lfsparms_V2);
    free(copy);
    if (ret) {
        *engine_error = ret;
        finish_locked();
        return 3;
    }
    *count = minutiae->num;
    if (*count <= capacity)
        for (i = 0; i < *count; i++) {
            MINUTIA *m = minutiae->list[i];
            lfs2nist_minutia_XYT(&output[i].x, &output[i].y, &output[i].angle, m, width, height);
            output[i].quality = (int)(m->reliability * 100.0 + 0.5);
            output[i].type = m->type == RIDGE_ENDING ? 1 : 2;
        }
    free_minutiae(minutiae);
    free(q);
    free(d);
    free(c);
    free(f);
    free(h);
    free(binary);
    finish_locked();
    return *count > capacity ? 2 : 0;
}
static int prepare(const op_minutia *input, int count, struct xyt_struct *out) {
    struct minutiae_struct sorted[MAX_FILE_MINUTIAE];
    int qualities[MAX_FILE_MINUTIAE], order[MAX_FILE_MINUTIAE], i, n;
    if (count < 0 || count > MAX_FILE_MINUTIAE || (count && !input))
        return 1;
    for (i = 0; i < count; i++) {
        if (input[i].x < 0 || input[i].x > 2048 || input[i].y < 0 || input[i].y > 2048 ||
            input[i].angle < 0 || input[i].angle >= 360 || input[i].quality < 0 ||
            input[i].quality > 100)
            return 1;
        qualities[i] = input[i].quality;
        order[i] = i;
    }
    if (count > max_minutiae && sort_order_decreasing(qualities, count, order))
        return 3;
    n = count > max_minutiae ? max_minutiae : count;
    for (i = 0; i < n; i++) {
        const op_minutia *m = &input[order[i]];
        sorted[i].col[0] = m->x;
        sorted[i].col[1] = m->y;
        sorted[i].col[2] = m->angle > 180 ? m->angle - 360 : m->angle;
        sorted[i].col[3] = m->quality;
    }
    qsort(sorted, (size_t)n, sizeof(sorted[0]), sort_x_y);
    out->nrows = n;
    for (i = 0; i < n; i++) {
        out->xcol[i] = sorted[i].col[0];
        out->ycol[i] = sorted[i].col[1];
        out->thetacol[i] = sorted[i].col[2];
    }
    return 0;
}
int32_t op_nbis_match(const op_minutia *probe, int32_t probe_count, const op_minutia *gallery,
                      int32_t gallery_count, int32_t *score) {
    struct xyt_struct p, g;
    int status;
    if (!score)
        return 1;
    *score = 0;
    LOCK();
    if (setjmp(op_allocation_failure)) {
        finish_locked();
        return 3;
    }
    errorfp = stderr;
    status = prepare(probe, probe_count, &p);
    if (!status)
        status = prepare(gallery, gallery_count, &g);
    if (!status && p.nrows >= min_computable_minutiae && g.nrows >= min_computable_minutiae)
        *score = bozorth_main(&p, &g);
    finish_locked();
    return status;
}

#ifdef OP_NBIS_TESTING
OP_API void op_nbis_test_fail_after(int count) {
    LOCK();
    op_fail_allocation_after(count);
    UNLOCK();
}
#endif
