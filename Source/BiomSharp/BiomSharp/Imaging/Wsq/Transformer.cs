// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BiomSharp.Imaging.Wsq.Segment;
using BiomSharp.Imaging.Wsq.Tree;

namespace BiomSharp.Imaging.Wsq
{
    internal class Transformer
    {
        private Transformer() { }

        private static void Reconstruct(WsqPixelBuffer<float> image, Dtt dtt, Quantizer quantizer,
            ArrayPool<float> scratchPool)
        {
            if (quantizer.WTree != null)
            {
                // Prepare synthesis coefficients once without modifying the shared DTT.
                Span<float> negatedHi = stackalloc float[dtt.L0 % 2 == 0 ? dtt.L1 : 0];
                scoped ReadOnlySpan<float> hi = dtt.DttL1.Span;
                if (dtt.L0 % 2 == 0)
                {
                    // L1 is a byte, so this per-reconstruction buffer is bounded to 255 floats.
                    for (int i = 0; i < negatedHi.Length; i++)
                    {
                        negatedHi[i] = -hi[i];
                    }
                    hi = negatedHi;
                }

                float[] rented = scratchPool.Rent(image.Length);
                try
                {
                    Span<float> temp = rented.AsSpan(0, image.Length);
                    Span<float> pixels = image.Pixels.AsSpan(0, image.Length);
                    ReadOnlySpan<float> lo = dtt.DttL0.Span;
                    WTree wtree = quantizer.WTree;
                    for (int node = WTree.Length - 1; node >= 0; node--)
                    {
                        int idx = (wtree[node].Y * image.Width) + wtree[node].X;
                        JoinLets(temp, pixels, 0, idx,
                            wtree[node].LenX, wtree[node].LenY,
                            1, image.Width,
                            hi, dtt.L1,
                            lo, dtt.L0,
                            wtree[node].InvC);
                        JoinLets(pixels, temp, idx, 0,
                            wtree[node].LenY, wtree[node].LenX,
                            image.Width, 1,
                            hi, dtt.L1,
                            lo, dtt.L0,
                            wtree[node].InvR);
                    }
                }
                finally
                {
                    scratchPool.Return(rented, clearArray: true);
                }
            }
            else
            {
                throw new WsqCodecException("Quantizer WTree is null or invalid");
            }
        }

        private static void JoinLets(
        Span<float> newdata,
        ReadOnlySpan<float> olddata,
        int newIndex,
        int oldIndex,
        int len1,
        int len2,
        int pitch,
        int stride,
        ReadOnlySpan<float> hi,
        int hsz,
        ReadOnlySpan<float> lo,
        int lsz,
        int inv)
        {
            int lp0, lp1;
            int hp0, hp1;
            int lopass, hipass;
            int limg, himg;
            int pix, cl_rw;
            int i, da_ev;
            int loc, hoc;
            int hlen, llen;
            int nstr, pstr;
            int tap;
            int olle, ohle, olre, ohre;
            int lle, lle2, lre, lre2;
            int hle, hle2, hre, hre2;
            int lpx, lspx;
            int lpxstr, lspxstr;
            int lstap, lotap;
            int hpx, hspx;
            int hpxstr, hspxstr;
            int hstap, hotap;
            int asym, fhre = 0, ofhre;
            float ssfac, osfac, sfac;

            da_ev = len2 % 2;
            bool evenLowPassLength = lsz % 2 == 0;
            pstr = stride;
            nstr = -pstr;
            if (da_ev != 0)
            {
                llen = (len2 + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = len2 / 2;
                hlen = llen;
            }

            if (!evenLowPassLength)
            {
                asym = 0;
                ssfac = 1F;
                ofhre = 0;
                loc = (lsz - 1) / 4;
                hoc = ((hsz + 1) / 4) - 1;
                lotap = (lsz - 1) / 2 % 2;
                hotap = (hsz + 1) / 2 % 2;
                if (da_ev != 0)
                {
                    olle = 0;
                    olre = 0;
                    ohle = 1;
                    ohre = 1;
                }
                else
                {
                    olle = 0;
                    olre = 1;
                    ohle = 1;
                    ohre = 0;
                }
            }
            else
            {
                asym = 1;
                ssfac = -1F;
                ofhre = 2;
                loc = (lsz / 4) - 1;
                hoc = (hsz / 4) - 1;
                lotap = lsz / 2 % 2;
                hotap = hsz / 2 % 2;
                if (da_ev != 0)
                {
                    olle = 1;
                    olre = 0;
                    ohle = 1;
                    ohre = 1;
                }
                else
                {
                    olle = 1;
                    olre = 1;
                    ohle = 1;
                    ohre = 1;
                }

                if (loc == -1)
                {
                    loc = 0;
                    olle = 0;
                }
                if (hoc == -1)
                {
                    hoc = 0;
                    ohle = 0;
                }
            }

            for (cl_rw = 0; cl_rw < len1; cl_rw++)
            {
                limg = newIndex + (cl_rw * pitch);
                himg = limg;
                newdata[himg] = 0F;
                newdata[himg + stride] = 0F;
                if (inv != 0)
                {
                    hipass = oldIndex + (cl_rw * pitch);
                    lopass = hipass + (stride * hlen);
                }
                else
                {
                    lopass = oldIndex + (cl_rw * pitch);
                    hipass = lopass + (stride * llen);
                }


                lp0 = lopass;
                lp1 = lp0 + ((llen - 1) * stride);
                lspx = lp0 + (loc * stride);
                lspxstr = nstr;
                lstap = lotap;
                lle2 = olle;
                lre2 = olre;

                hp0 = hipass;
                hp1 = hp0 + ((hlen - 1) * stride);
                hspx = hp0 + (hoc * stride);
                hspxstr = nstr;
                hstap = hotap;
                hle2 = ohle;
                hre2 = ohre;
                osfac = ssfac;

                for (pix = 0; pix < hlen; pix++)
                {
                    for (tap = lstap; tap >= 0; tap--)
                    {
                        lle = lle2;
                        lre = lre2;
                        lpx = lspx;
                        lpxstr = lspxstr;

                        float lowPassValue = olddata[lpx] * lo[tap];
                        for (i = tap + 2; i < lsz; i += 2)
                        {
                            if (lpx == lp0)
                            {
                                if (lle != 0)
                                {
                                    lpxstr = 0;
                                    lle = 0;
                                }
                                else
                                {
                                    lpxstr = pstr;
                                }
                            }
                            if (lpx == lp1)
                            {
                                if (lre != 0)
                                {
                                    lpxstr = 0;
                                    lre = 0;
                                }
                                else
                                {
                                    lpxstr = nstr;
                                }
                            }
                            lpx += lpxstr;
                            lowPassValue += olddata[lpx] * lo[i];
                        }
                        newdata[limg] = lowPassValue;
                        limg += stride;
                    }
                    if (lspx == lp0)
                    {
                        if (lle2 != 0)
                        {
                            lspxstr = 0;
                            lle2 = 0;
                        }
                        else
                        {
                            lspxstr = pstr;
                        }
                    }
                    lspx += lspxstr;
                    lstap = 1;

                    for (tap = hstap; tap >= 0; tap--)
                    {
                        hle = hle2;
                        hre = hre2;
                        hpx = hspx;
                        hpxstr = hspxstr;
                        fhre = ofhre;
                        sfac = osfac;

                        float highPassValue = newdata[himg];
                        for (i = tap; i < hsz; i += 2)
                        {
                            if (hpx == hp0)
                            {
                                if (hle != 0)
                                {
                                    hpxstr = 0;
                                    hle = 0;
                                }
                                else
                                {
                                    hpxstr = pstr;
                                    sfac = 1.0f;
                                }
                            }
                            if (hpx == hp1)
                            {
                                if (hre != 0)
                                {
                                    hpxstr = 0;
                                    hre = 0;
                                    if (asym != 0 && da_ev != 0)
                                    {
                                        hre = 1;
                                        fhre--;
                                        sfac = fhre;
                                        if (sfac == 0.0)
                                        {
                                            hre = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    hpxstr = nstr;
                                    if (asym != 0)
                                    {
                                        sfac = -1F;
                                    }
                                }
                            }
                            highPassValue += olddata[hpx] * hi[i] * sfac;
                            hpx += hpxstr;
                        }
                        newdata[himg] = highPassValue;
                        himg += stride;
                    }
                    if (hspx == hp0)
                    {
                        if (hle2 != 0)
                        {
                            hspxstr = 0;
                            hle2 = 0;
                        }
                        else
                        {
                            hspxstr = pstr;
                            osfac = 1F;
                        }
                    }
                    hspx += hspxstr;
                    hstap = 1;
                }


                if (da_ev != 0)
                {
                    if (lotap != 0)
                    {
                        lstap = 1;
                    }
                    else
                    {
                        lstap = 0;
                    }
                }
                else if (lotap != 0)
                {
                    lstap = 2;
                }
                else
                {
                    lstap = 1;
                }

                for (tap = 1; tap >= lstap; tap--)
                {
                    lle = lle2;
                    lre = lre2;
                    lpx = lspx;
                    lpxstr = lspxstr;

                    float lowPassValue = olddata[lpx] * lo[tap];
                    for (i = tap + 2; i < lsz; i += 2)
                    {
                        if (lpx == lp0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }
                        if (lpx == lp1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }
                        lpx += lpxstr;
                        lowPassValue += olddata[lpx] * lo[i];
                    }
                    newdata[limg] = lowPassValue;
                    limg += stride;
                }

                if (da_ev != 0)
                {
                    if (hotap != 0)
                    {
                        hstap = 1;
                    }
                    else
                    {
                        hstap = 0;
                    }

                    if (hsz == 2)
                    {
                        hspx -= hspxstr;
                        fhre = 1;
                    }
                }
                else if (hotap != 0)
                {
                    hstap = 2;
                }
                else
                {
                    hstap = 1;
                }

                for (tap = 1; tap >= hstap; tap--)
                {
                    hle = hle2;
                    hre = hre2;
                    hpx = hspx;
                    hpxstr = hspxstr;
                    sfac = osfac;
                    if (hsz != 2)
                    {
                        fhre = ofhre;
                    }

                    float highPassValue = newdata[himg];
                    for (i = tap; i < hsz; i += 2)
                    {
                        if (hpx == hp0)
                        {
                            if (hle != 0)
                            {
                                hpxstr = 0;
                                hle = 0;
                            }
                            else
                            {
                                hpxstr = pstr;
                                sfac = 1.0f;
                            }
                        }
                        if (hpx == hp1)
                        {
                            if (hre != 0)
                            {
                                hpxstr = 0;
                                hre = 0;
                                if (asym != 0 && da_ev != 0)
                                {
                                    hre = 1;
                                    fhre--;
                                    sfac = fhre;
                                    if (sfac == 0F)
                                    {
                                        hre = 0;
                                    }
                                }
                            }
                            else
                            {
                                hpxstr = nstr;
                                if (asym != 0)
                                {
                                    sfac = -1F;
                                }
                            }
                        }
                        highPassValue += olddata[hpx] * hi[i] * sfac;
                        hpx += hpxstr;
                    }
                    newdata[himg] = highPassValue;
                    himg += stride;
                }
            }
        }

        private static void Deconstruct(WsqPixelBuffer<float> image, Quantizer quantizer,
            Filter filter, ArrayPool<float> scratchPool)
        {
            if (quantizer.WTree != null)
            {
                float[] rented = scratchPool.Rent(image.Length);
                try
                {
                    Span<float> temp = rented.AsSpan(0, image.Length);
                    Span<float> pixels = image.Pixels.AsSpan(0, image.Length);
                    WTree wtree = quantizer.WTree;
                    for (int node = 0; node < WTree.Length; node++)
                    {
                        int idx = (wtree[node].Y * image.Width) + wtree[node].X;
                        GetLets(temp, pixels, 0, idx,
                            wtree[node].LenY, wtree[node].LenX,
                            image.Width, 1,
                            filter.Hi.Span, filter.Lo.Span, wtree[node].InvR);
                        GetLets(pixels, temp, idx, 0,
                            wtree[node].LenX, wtree[node].LenY,
                            1, image.Width,
                            filter.Hi.Span, filter.Lo.Span, wtree[node].InvC);
                    }
                }
                finally
                {
                    // Do not retain fingerprint-derived coefficients in the shared pool.
                    scratchPool.Return(rented, clearArray: true);
                }
            }
            else
            {
                throw new WsqCodecException(
                    "Quantizer WTree is null or invalid");
            }
        }


        private static void GetLets(
            Span<float> newdata,
            ReadOnlySpan<float> olddata,
            int newIndex,
            int oldIndex,
            int len1,
            int len2,
            int pitch,
            int stride,
            ReadOnlySpan<float> hiflt,  // OPTIMIZATION: Changed from IReadOnlyList to ReadOnlySpan to eliminate virtual interface dispatch and enable JIT devirtualization/unrolling
            ReadOnlySpan<float> loflt,  // OPTIMIZATION: Changed from IReadOnlyList to ReadOnlySpan to eliminate virtual interface dispatch and enable JIT devirtualization/unrolling
            int inv)
        {
            int lopass_idx, hipass_idx;
            int p0_idx, p1_idx;
            int da_ev;
            int loc, hoc, nstr, pstr;
            int llen, hlen;
            int lpxstr, lspxstr;
            int lpxIndex, lspxIndex;
            int hpxstr, hspxstr;
            int hpxIndex, hspxIndex;
            int olle, ohle;
            int olre, ohre;
            int lle, lle2;
            int lre, lre2;
            int hle, hle2;
            int hre, hre2;

            da_ev = len2 % 2;
            bool negateHighPassCoefficients = loflt.Length % 2 == 0;

            if (!negateHighPassCoefficients)
            {
                loc = (loflt.Length - 1) / 2;
                hoc = ((hiflt.Length - 1) / 2) - 1;
                olle = 0;
                ohle = 0;
                olre = 0;
                ohre = 0;
            }
            else
            {
                loc = (loflt.Length / 2) - 2;
                hoc = (hiflt.Length / 2) - 2;
                olle = 1;
                ohle = 1;
                olre = 1;
                ohre = 1;

                if (loc == -1)
                { loc = 0; olle = 0; }
                if (hoc == -1)
                { hoc = 0; ohle = 0; }
            }

            pstr = stride;
            nstr = -pstr;

            if (da_ev != 0)
            {
                llen = (len2 + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = len2 / 2;
                hlen = llen;
            }

            /*
             * COMPLEX DECISION: PRE-ALLOCATING AND ALIGNING NEGATED HIGH-PASS COEFFICIENTS
             * 
             * In the original code, the ternary operation `negateHighPassCoefficients ? -hiflt[i] : hiflt[i]` 
             * executed inside the deepest loop millions of times. This introduces severe branch prediction overhead 
             * and invalidates CPU instruction pipelining.
             * 
             * To solve this, we precompute a local buffer containing the exact high-pass coefficient values 
             * needed for processing. Since WSQ filter sizes are small (typically < 32 elements), we can allocate 
             * this safely on the execution stack using `stackalloc` to guarantee L1-cache speed and avoid any GC pressure.
             */
            Span<float> negatedHi = stackalloc float[negateHighPassCoefficients ? hiflt.Length : 0];

            scoped ReadOnlySpan<float> activeHiFlt = hiflt;

            if (negateHighPassCoefficients)
            {
                for (int i = 0; i < hiflt.Length; i++)
                {
                    negatedHi[i] = -hiflt[i];
                }

                activeHiFlt = negatedHi;
            }

            /*
             * CRITICAL PERFORMANCE OPTIMIZATION: BYPASSING JIT BOUNDS CHECKING VIA UNSAFE MEMORY REFS
             * 
             * Even when working with managed `Span<T>`, the .NET JIT compiler emits index-out-of-bounds checks 
             * inside loops unless it can statistically guarantee that boundaries won't be breached. 
             * Because WSQ tracking utilizes highly dynamic offset pointers (`lpxIndex`, `lopass_idx`, etc.), 
             * the compiler fails to optimize them, introducing a penalty check on every single pixel.
             * 
             * By pinning the base addresses as managed references via `MemoryMarshal.GetReference`, we can use 
             * `Unsafe.Add` to jump precisely to target memory locations, executing purely at native C/C++ hardware speeds.
             */
            ref float newDataBase = ref MemoryMarshal.GetReference(newdata);
            ref float oldDataBase = ref MemoryMarshal.GetReference(olddata);

            for (int rw_cl = 0; rw_cl < len1; rw_cl++)
            {
                if (inv != 0)
                {
                    hipass_idx = newIndex + (rw_cl * pitch);
                    lopass_idx = hipass_idx + (hlen * stride);
                }
                else
                {
                    lopass_idx = newIndex + (rw_cl * pitch);
                    hipass_idx = lopass_idx + (llen * stride);
                }

                p0_idx = oldIndex + (rw_cl * pitch);
                p1_idx = p0_idx + ((len2 - 1) * stride);

                lspxIndex = p0_idx + (loc * stride);
                lspxstr = nstr;
                lle2 = olle;
                lre2 = olre;
                hspxIndex = p0_idx + (hoc * stride);
                hspxstr = nstr;
                hle2 = ohle;
                hre2 = ohre;

                for (int pix = 0; pix < hlen; pix++)
                {
                    lpxstr = lspxstr;
                    lpxIndex = lspxIndex;
                    lle = lle2;
                    lre = lre2;

                    // Using Unsafe read/write to drastically lower CPU clock execution per pixel
                    Unsafe.Add(ref newDataBase, lopass_idx) = Unsafe.Add(ref oldDataBase, lpxIndex) * loflt[0];

                    for (int i = 1; i < loflt.Length; i++)
                    {
                        if (lpxIndex == p0_idx)
                        {
                            if (lle != 0)
                            { lpxstr = 0; lle = 0; }
                            else
                            { lpxstr = pstr; }
                        }
                        if (lpxIndex == p1_idx)
                        {
                            if (lre != 0)
                            { lpxstr = 0; lre = 0; }
                            else
                            { lpxstr = nstr; }
                        }
                        lpxIndex += lpxstr;
                        Unsafe.Add(ref newDataBase, lopass_idx) += Unsafe.Add(ref oldDataBase, lpxIndex) * loflt[i];
                    }
                    lopass_idx += stride;

                    hpxstr = hspxstr;
                    hpxIndex = hspxIndex;
                    hle = hle2;
                    hre = hre2;

                    // Performance gain: Using the pre-processed activeHiFlt directly without evaluations
                    Unsafe.Add(ref newDataBase, hipass_idx) = Unsafe.Add(ref oldDataBase, hpxIndex) * activeHiFlt[0];

                    for (int i = 1; i < activeHiFlt.Length; i++)
                    {
                        if (hpxIndex == p0_idx)
                        {
                            if (hle != 0)
                            { hpxstr = 0; hle = 0; }
                            else
                            { hpxstr = pstr; }
                        }
                        if (hpxIndex == p1_idx)
                        {
                            if (hre != 0)
                            { hpxstr = 0; hre = 0; }
                            else
                            { hpxstr = nstr; }
                        }
                        hpxIndex += hpxstr;
                        Unsafe.Add(ref newDataBase, hipass_idx) += Unsafe.Add(ref oldDataBase, hpxIndex) * activeHiFlt[i];
                    }
                    hipass_idx += stride;

                    for (int i = 0; i < 2; i++)
                    {
                        if (lspxIndex == p0_idx)
                        {
                            if (lle2 != 0)
                            { lspxstr = 0; lle2 = 0; }
                            else
                            { lspxstr = pstr; }
                        }
                        lspxIndex += lspxstr;

                        if (hspxIndex == p0_idx)
                        {
                            if (hle2 != 0)
                            { hspxstr = 0; hle2 = 0; }
                            else
                            { hspxstr = pstr; }
                        }
                        hspxIndex += hspxstr;
                    }
                }

                if (da_ev != 0)
                {
                    lpxstr = lspxstr;
                    lpxIndex = lspxIndex;
                    lle = lle2;
                    lre = lre2;

                    Unsafe.Add(ref newDataBase, lopass_idx) = Unsafe.Add(ref oldDataBase, lpxIndex) * loflt[0];

                    for (int i = 1; i < loflt.Length; i++)
                    {
                        if (lpxIndex == p0_idx)
                        {
                            if (lle != 0)
                            { lpxstr = 0; lle = 0; }
                            else
                            { lpxstr = pstr; }
                        }
                        if (lpxIndex == p1_idx)
                        {
                            if (lre != 0)
                            { lpxstr = 0; lre = 0; }
                            else
                            { lpxstr = nstr; }
                        }
                        lpxIndex += lpxstr;
                        Unsafe.Add(ref newDataBase, lopass_idx) += Unsafe.Add(ref oldDataBase, lpxIndex) * loflt[i];
                    }
                }
            }
        }

        // JV TODO: delete
        private static void GetLets_old(
            Span<float> newdata,
            ReadOnlySpan<float> olddata,
            int newIndex,
            int oldIndex,
            int len1,
            int len2,
            int pitch,
            int stride,
            ReadOnlySpan<float> hiflt,
            ReadOnlySpan<float> loflt,
            int inv)
        {
            int lopass_idx, hipass_idx;
            int p0_idx, p1_idx;
            int da_ev;
            int loc, hoc, nstr, pstr;
            int llen, hlen;
            int lpxstr, lspxstr;
            int lpxIndex, lspxIndex;
            int hpxstr, hspxstr;
            int hpxIndex, hspxIndex;
            int olle, ohle;
            int olre, ohre;
            int lle, lle2;
            int lre, lre2;
            int hle, hle2;
            int hre, hre2;

            da_ev = len2 % 2;
            bool negateHighPassCoefficients = loflt.Length % 2 == 0;

            if (!negateHighPassCoefficients)
            {
                loc = (loflt.Length - 1) / 2;
                hoc = ((hiflt.Length - 1) / 2) - 1;
                olle = 0;
                ohle = 0;
                olre = 0;
                ohre = 0;
            }
            else
            {
                loc = (loflt.Length / 2) - 2;
                hoc = (hiflt.Length / 2) - 2;
                olle = 1;
                ohle = 1;
                olre = 1;
                ohre = 1;

                if (loc == -1)
                {
                    loc = 0;
                    olle = 0;
                }
                if (hoc == -1)
                {
                    hoc = 0;
                    ohle = 0;
                }
            }

            pstr = stride;
            nstr = -pstr;

            if (da_ev != 0)
            {
                llen = (len2 + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = len2 / 2;
                hlen = llen;
            }

            for (int rw_cl = 0; rw_cl < len1; rw_cl++)
            {
                if (inv != 0)
                {
                    hipass_idx = newIndex + (rw_cl * pitch);
                    lopass_idx = hipass_idx + (hlen * stride);
                }
                else
                {
                    lopass_idx = newIndex + (rw_cl * pitch);
                    hipass_idx = lopass_idx + (llen * stride);
                }

                p0_idx = oldIndex + (rw_cl * pitch);
                p1_idx = p0_idx + ((len2 - 1) * stride);

                lspxIndex = p0_idx + (loc * stride);
                lspxstr = nstr;
                lle2 = olle;
                lre2 = olre;
                hspxIndex = p0_idx + (hoc * stride);
                hspxstr = nstr;
                hle2 = ohle;
                hre2 = ohre;
                for (int pix = 0; pix < hlen; pix++)
                {
                    lpxstr = lspxstr;
                    lpxIndex = lspxIndex;
                    lle = lle2;
                    lre = lre2;
                    newdata[lopass_idx] = olddata[lpxIndex] * loflt[0];
                    for (int i = 1; i < loflt.Length; i++)
                    {
                        if (lpxIndex == p0_idx)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }
                        if (lpxIndex == p1_idx)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }
                        lpxIndex += lpxstr;
                        newdata[lopass_idx] += olddata[lpxIndex] * loflt[i];
                    }
                    lopass_idx += stride;

                    hpxstr = hspxstr;
                    hpxIndex = hspxIndex;
                    hle = hle2;
                    hre = hre2;
                    float highPassCoefficient = negateHighPassCoefficients ? -hiflt[0] : hiflt[0];
                    newdata[hipass_idx] = olddata[hpxIndex] * highPassCoefficient;
                    for (int i = 1; i < hiflt.Length; i++)
                    {
                        if (hpxIndex == p0_idx)
                        {
                            if (hle != 0)
                            {
                                hpxstr = 0;
                                hle = 0;
                            }
                            else
                            {
                                hpxstr = pstr;
                            }
                        }
                        if (hpxIndex == p1_idx)
                        {
                            if (hre != 0)
                            {
                                hpxstr = 0;
                                hre = 0;
                            }
                            else
                            {
                                hpxstr = nstr;
                            }
                        }
                        hpxIndex += hpxstr;
                        highPassCoefficient = negateHighPassCoefficients ? -hiflt[i] : hiflt[i];
                        newdata[hipass_idx] += olddata[hpxIndex] * highPassCoefficient;
                    }
                    hipass_idx += stride;

                    for (int i = 0; i < 2; i++)
                    {
                        if (lspxIndex == p0_idx)
                        {
                            if (lle2 != 0)
                            {
                                lspxstr = 0;
                                lle2 = 0;
                            }
                            else
                            {
                                lspxstr = pstr;
                            }
                        }
                        lspxIndex += lspxstr;
                        if (hspxIndex == p0_idx)
                        {
                            if (hle2 != 0)
                            {
                                hspxstr = 0;
                                hle2 = 0;
                            }
                            else
                            {
                                hspxstr = pstr;
                            }
                        }
                        hspxIndex += hspxstr;
                    }
                }
                if (da_ev != 0)
                {
                    lpxstr = lspxstr;
                    lpxIndex = lspxIndex;
                    lle = lle2;
                    lre = lre2;
                    newdata[lopass_idx] = olddata[lpxIndex] * loflt[0];
                    for (int i = 1; i < loflt.Length; i++)
                    {
                        if (lpxIndex == p0_idx)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }
                        if (lpxIndex == p1_idx)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }
                        lpxIndex += lpxstr;
                        newdata[lopass_idx] += olddata[lpxIndex] * loflt[i];
                    }
                    //lopass_idx += stride;
                }
            }
        }

        internal static Transformer Construct(WsqPixelBuffer<float> image, Dtt dtt, Quantizer quantizer)
        {
            return Construct(image, dtt, quantizer, ArrayPool<float>.Shared);
        }

        internal static Transformer Construct(WsqPixelBuffer<float> image, Dtt dtt, Quantizer quantizer,
            ArrayPool<float> scratchPool)
        {
            ArgumentNullException.ThrowIfNull(scratchPool);
            var transformer = new Transformer();
            Reconstruct(image, dtt, quantizer, scratchPool);
            return transformer;
        }

        internal static Transformer Decompose(WsqPixelBuffer<float> image, Quantizer quantizer, Filter filter)
        {
            return Decompose(image, quantizer, filter, ArrayPool<float>.Shared);
        }

        internal static Transformer Decompose(WsqPixelBuffer<float> image, Quantizer quantizer, Filter filter, ArrayPool<float> scratchPool)
        {
            var transformer = new Transformer();
            Deconstruct(image, quantizer, filter, scratchPool);
            return transformer;
        }
    }
}
