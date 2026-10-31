/* SPDX-License-Identifier: GPL-2.0-or-later
 * Copyright (c) 2026 Mistial Developer
 */
#include "munit.h"
#include "op_nbis.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
    char id[32];
    int32_t count;
    op_minutia points[1000];
} sd04_template;

static FILE *sd04_open(const char *name) {
    char path[4096];
    snprintf(path, sizeof(path), "%s/sd04/%s", NBIS_FIXTURE_DIR, name);
    FILE *file = fopen(path, "rb");
    munit_assert_not_null(file);
    return file;
}

MunitResult sd04_reference_parity(const MunitParameter params[], void *data) {
    (void)params;
    (void)data;
    sd04_template *templates = munit_malloc(20 * sizeof(sd04_template));
    memset(templates, 0, 20 * sizeof(sd04_template));
    uint8_t *pixels = munit_malloc(512 * 512);
    FILE *index = sd04_open("images.tsv");
    char id[32], raw[128], xyt[128];
    int width, height, dpi, count = 0;
    while (fscanf(index, "%31s %127s %127s %d %d %d", id, raw, xyt, &width, &height, &dpi) == 6) {
        munit_assert_int(count, <, 20);
        munit_assert_int(width, ==, 512);
        munit_assert_int(height, ==, 512);
        munit_assert_int(dpi, ==, 500);
        sd04_template *current = &templates[count++];
        strcpy(current->id, id);
        FILE *file = sd04_open(raw);
        munit_assert_size(fread(pixels, 1, 512 * 512, file), ==, 512 * 512);
        munit_assert_int(fgetc(file), ==, EOF);
        fclose(file);
        int32_t error;
        munit_assert_int(op_nbis_extract(pixels, 512 * 512, width, height, dpi, current->points,
                                         1000, &current->count, &error),
                         ==, 0);
        file = sd04_open(xyt);
        int x, y, angle, quality, lines = 0;
        while (fscanf(file, "%d %d %d %d", &x, &y, &angle, &quality) == 4) {
            munit_assert_int(lines, <, current->count);
            op_minutia *actual = &current->points[lines++];
            munit_assert_int(actual->x, ==, x);
            munit_assert_int(actual->y, ==, y);
            munit_assert_int(actual->angle, ==, angle);
            munit_assert_int(actual->quality, ==, quality);
        }
        munit_assert_int(lines, ==, current->count);
        fclose(file);
    }
    fclose(index);
    munit_assert_int(count, ==, 20);
    FILE *file = sd04_open("scores.tsv");
    char probe[32], gallery[32];
    int expected, cases = 0;
    int self_cases = 0, paired_cases = 0, different_finger_cases = 0;
    int nonzero_different_finger_cases = 0;
    unsigned char seen[20][20] = {{0}};
    while (fscanf(file, "%31s %31s %d", probe, gallery, &expected) == 3) {
        int p = -1, g = -1;
        for (int i = 0; i < 20; i++) {
            if (strcmp(probe, templates[i].id) == 0)
                p = i;
            if (strcmp(gallery, templates[i].id) == 0)
                g = i;
        }
        munit_assert_int(p, >=, 0);
        munit_assert_int(g, >=, 0);
        munit_assert_int(seen[p][g], ==, 0);
        seen[p][g] = 1;
        int32_t score;
        munit_assert_int(op_nbis_match(templates[p].points, templates[p].count, templates[g].points,
                                       templates[g].count, &score),
                         ==, 0);
        munit_assert_int(score, ==, expected);
        /* Prefixes distinguish impressions; the remaining identifier names the finger.
         * Count non-mates explicitly so fixture edits cannot erase negative coverage. */
        if (p == g) {
            self_cases++;
        } else if (strcmp(probe + 1, gallery + 1) == 0) {
            paired_cases++;
        } else {
            different_finger_cases++;
            if (score > 0)
                nonzero_different_finger_cases++;
        }
        cases++;
    }
    fclose(file);
    munit_assert_int(cases, ==, 400);
    munit_assert_int(self_cases, ==, 20);
    munit_assert_int(paired_cases, ==, 20);
    munit_assert_int(different_finger_cases, ==, 360);
    munit_assert_int(nonzero_different_finger_cases, >, 0);
    free(pixels);
    free(templates);
    return MUNIT_OK;
}
