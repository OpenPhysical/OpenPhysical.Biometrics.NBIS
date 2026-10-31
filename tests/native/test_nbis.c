/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */

#include "munit.h"
#include "op_nbis.h"
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
MunitResult sd04_reference_parity(const MunitParameter params[], void *data);
#ifdef _WIN32
__declspec(dllimport) void op_nbis_test_fail_after(int);
#else
void op_nbis_test_fail_after(int);
#endif

typedef struct {
    uint8_t *pixels;
    op_minutia *output;
} fixture_data;
static FILE *open_fixture(const char *name) {
    char path[4096];
    snprintf(path, sizeof(path), "%s/%s", NBIS_FIXTURE_DIR, name);
    FILE *f = fopen(path, "rb");
    munit_assert_not_null(f);
    return f;
}
static void *setup(const MunitParameter params[], void *data) {
    (void)params;
    (void)data;
    fixture_data *f = munit_malloc(sizeof(*f));
    f->pixels = munit_malloc(800 * 768);
    f->output = munit_malloc(1000 * sizeof(op_minutia));
    FILE *file = open_fixture("g001t2u.gray");
    munit_assert_size(fread(f->pixels, 1, 800 * 768, file), ==, 800 * 768);
    fclose(file);
    op_nbis_test_fail_after(-1);
    return f;
}
static void teardown(void *data) {
    fixture_data *f = data;
    op_nbis_test_fail_after(-1);
    free(f->pixels);
    free(f->output);
    free(f);
}
static MunitResult extraction_parity(const MunitParameter params[], void *data) {
    (void)params;
    fixture_data *f = data;
    int32_t count, error;
    op_minutia expected;
    int lines = 0;
    munit_assert_int(
        op_nbis_extract(f->pixels, 800 * 768, 800, 768, 500, f->output, 1000, &count, &error), ==,
        0);
    FILE *file = open_fixture("g001t2u_nist.xyt");
    while (fscanf(file, "%d %d %d %d", &expected.x, &expected.y, &expected.angle,
                  &expected.quality) == 4) {
        munit_assert_int(lines, <, count);
        munit_assert_int(f->output[lines].x, ==, expected.x);
        munit_assert_int(f->output[lines].y, ==, expected.y);
        munit_assert_int(f->output[lines].angle, ==, expected.angle);
        munit_assert_int(f->output[lines].quality, ==, expected.quality);
        lines++;
    }
    fclose(file);
    munit_assert_int(count, ==, 280);
    munit_assert_int(lines, ==, count);
    return MUNIT_OK;
}
static int load_xyt(const char *name, op_minutia *points) {
    FILE *f = open_fixture(name);
    int n = 0;
    while (n < 1000 && fscanf(f, "%d %d %d %d", &points[n].x, &points[n].y, &points[n].angle,
                              &points[n].quality) == 4) {
        points[n].type = 0;
        n++;
    }
    fclose(f);
    return n;
}
static MunitResult matching_parity(const MunitParameter params[], void *data) {
    (void)params;
    (void)data;
    op_minutia p[1000], g[1000];
    char pn[100], gn[100];
    int expected;
    FILE *f = open_fixture("1to1_1_nist.log");
    while (fscanf(f, "%99s %99s %d", pn, gn, &expected) == 3) {
        int pc = load_xyt(strrchr(pn, '/') + 1, p), gc = load_xyt(strrchr(gn, '/') + 1, g);
        int32_t score;
        munit_assert_int(op_nbis_match(p, pc, g, gc, &score), ==, 0);
        munit_assert_int(score, ==, expected);
    }
    fclose(f);
    f = open_fixture("f2tog_nist.log");
    while (fscanf(f, "%99s %99s %d", pn, gn, &expected) == 3) {
        int pc = load_xyt(strrchr(pn, '/') + 1, p), gc = load_xyt(strrchr(gn, '/') + 1, g);
        int32_t score;
        munit_assert_int(op_nbis_match(p, pc, g, gc, &score), ==, 0);
        munit_assert_int(score, ==, expected);
    }
    fclose(f);
    return MUNIT_OK;
}
static MunitResult validation(const MunitParameter params[], void *data) {
    (void)params;
    fixture_data *f = data;
    int32_t count, error, score;
    munit_assert_int(op_nbis_abi_version(), ==, 1);
    munit_assert_int(
        op_nbis_extract(f->pixels, 800 * 768, 800, 768, 500, f->output, 1, &count, &error), ==, 2);
    munit_assert_int(
        op_nbis_extract(f->pixels, 800 * 768, INT32_MAX, 768, 500, f->output, 1000, &count, &error),
        ==, 1);
    munit_assert_int(op_nbis_extract(f->pixels, 1, 800, 768, 500, f->output, 1000, &count, &error),
                     ==, 1);
    munit_assert_int(op_nbis_extract(NULL, 1, 64, 64, 500, f->output, 1000, &count, &error), ==, 1);
    munit_assert_int(op_nbis_match(NULL, 0, NULL, 0, &score), ==, 0);
    munit_assert_int(score, ==, 0);
    munit_assert_int(op_nbis_match(f->output, 1001, f->output, 1, &score), ==, 1);
    memset(f->pixels, 255, 128 * 128);
    munit_assert_int(
        op_nbis_extract(f->pixels, 128 * 128, 128, 128, 500, f->output, 1000, &count, &error), ==,
        0);
    munit_assert_int(count, ==, 0);
    return MUNIT_OK;
}
static MunitResult allocation_failure(const MunitParameter params[], void *data) {
    (void)params;
    fixture_data *f = data;
    int32_t count, error;
    for (int i = 0; i < 80; i++) {
        op_nbis_test_fail_after(i);
        munit_assert_int(
            op_nbis_extract(f->pixels, 800 * 768, 800, 768, 500, f->output, 1000, &count, &error),
            ==, 3);
        munit_assert_int(error, ==, -10000);
    }
    op_nbis_test_fail_after(-1);
    munit_assert_int(
        op_nbis_extract(f->pixels, 800 * 768, 800, 768, 500, f->output, 1000, &count, &error), ==,
        0);
    munit_assert_int(count, ==, 280);
    return MUNIT_OK;
}
static MunitResult noisy_images(const MunitParameter params[], void *data) {
    (void)params;
    fixture_data *f = data;
    int32_t count, error;
    for (int i = 0; i < 20; i++) {
        uint32_t seed = (uint32_t)i + 1;
        for (int j = 0; j < 128 * 128; j++) {
            seed = 1664525 * seed + 1013904223;
            f->pixels[j] = (uint8_t)(seed >> 24);
        }
        munit_assert_int(
            op_nbis_extract(f->pixels, 128 * 128, 128, 128, 500, f->output, 1000, &count, &error),
            ==, 0);
    }
    return MUNIT_OK;
}
static MunitResult matching_allocation_recovery(const MunitParameter params[], void *data) {
    (void)params;
    (void)data;
    op_minutia p[1000], g[1000];
    int pc = load_xyt("a011r_03.xyt", p), gc = load_xyt("a011r_02.xyt", g);
    int32_t score, expected;
    munit_assert_int(pc, >, 150);
    munit_assert_int(gc, >, 150);
    op_nbis_test_fail_after(-1);
    munit_assert_int(op_nbis_match(p, pc, g, gc, &expected), ==, 0);
    /* One allocation per pruned template, before the upstream quicksort begins. */
    for (int i = 0; i < 2; i++) {
        op_nbis_test_fail_after(i);
        score = -1;
        munit_assert_int(op_nbis_match(p, pc, g, gc, &score), ==, 3);
        munit_assert_int(score, ==, 0);
        op_nbis_test_fail_after(-1);
        munit_assert_int(op_nbis_match(p, pc, g, gc, &score), ==, 0);
        munit_assert_int(score, ==, expected);
    }
    op_nbis_test_fail_after(2);
    munit_assert_int(op_nbis_match(p, pc, g, gc, &score), ==, 0);
    op_nbis_test_fail_after(-1);
    return MUNIT_OK;
}
static MunitResult maximum_count_sorting(const MunitParameter params[], void *data) {
    (void)params;
    (void)data;
    op_minutia points[1000];
    int32_t expected, score;
    for (int pattern = 0; pattern < 4; pattern++) {
        for (int i = 0; i < 1000; i++) {
            points[i] = (op_minutia){(i * 17) % 2048, (i * 31) % 2048, (i * 7) % 360,
                                     pattern == 0   ? 50
                                     : pattern == 1 ? i / 10
                                     : pattern == 2 ? 100 - i / 10
                                                    : (i * 73) % 101,
                                     0};
        }
        munit_assert_int(op_nbis_match(points, 1000, points, 1000, &expected), ==, 0);
        for (int i = 0; i < 5; i++) {
            munit_assert_int(op_nbis_match(points, 1000, points, 1000, &score), ==, 0);
            munit_assert_int(score, ==, expected);
        }
    }
    return MUNIT_OK;
}
static MunitTest tests[] = {
    {"/sd04-reference-parity", sd04_reference_parity, NULL, NULL, MUNIT_TEST_OPTION_NONE, NULL},
    {"/extraction-parity", extraction_parity, setup, teardown, MUNIT_TEST_OPTION_NONE, NULL},
    {"/matching-parity", matching_parity, NULL, NULL, MUNIT_TEST_OPTION_NONE, NULL},
    {"/validation", validation, setup, teardown, MUNIT_TEST_OPTION_NONE, NULL},
    {"/allocation-recovery", allocation_failure, setup, teardown, MUNIT_TEST_OPTION_NONE, NULL},
    {"/noisy-images", noisy_images, setup, teardown, MUNIT_TEST_OPTION_NONE, NULL},
    {"/matching-allocation-recovery", matching_allocation_recovery, NULL, NULL,
     MUNIT_TEST_OPTION_NONE, NULL},
    {"/maximum-count-sorting", maximum_count_sorting, NULL, NULL, MUNIT_TEST_OPTION_NONE, NULL},
    {NULL, NULL, NULL, NULL, MUNIT_TEST_OPTION_NONE, NULL}};
static const MunitSuite suite = {"/nbis", tests, NULL, 1, MUNIT_SUITE_OPTION_NONE};
int main(int argc, char **argv) { return munit_suite_main(&suite, NULL, argc, argv); }
