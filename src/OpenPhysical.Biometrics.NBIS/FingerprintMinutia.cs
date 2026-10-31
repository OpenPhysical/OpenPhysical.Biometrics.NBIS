// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

/// <summary>A minutia in NIST XYT coordinates at nominal 500 pixels per inch.</summary>
/// <param name="X">Pixel column from the left.</param>
/// <param name="Y">Height minus the top-origin pixel row, matching NBIS XYT output.</param>
/// <param name="Angle">Degrees counterclockwise from east, pointing away from the ridge ending.</param>
/// <param name="Quality">MINDTCT reliability or converted quality from 0 to 100.
/// For converted INCITS minutiae, zero means quality was unreported.</param>
/// <param name="Type">Unknown (0), ridge ending (1), or bifurcation (2); unused by Bozorth3.</param>
public sealed record FingerprintMinutia(int X, int Y, int Angle, int Quality, int Type = 0);
