# NIST NBIS 5.0.0 WSQ reference decoders

This directory contains locally compiled Windows versions of NIST's `dwsq`
command-line WSQ decoder. BiomSharp's imaging tests use them as an independent
native decoder for comparison with BiomSharp's managed WSQ implementation.

**These are local builds of NIST source code, not Windows executables distributed
or certified by NIST.** The project name is **NIST Biometric Image Software
(NBIS)**. The `_x32` and `_x64` filename suffixes were added locally to distinguish
the architectures; the upstream program is named `dwsq`.

## Source and authorship

The source was downloaded directly from NIST, not from a third-party GitHub
mirror:

- [NIST NBIS project page](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)
- [Official NBIS 5.0.0 source archive: nbis_v5_0_0.zip](https://nigos.nist.gov/nist/nbis/nbis_v5_0_0.zip)
- [NIST Biometric Open Source Server (NIGOS)](https://www.nist.gov/itl/iad/btg/products-and-services/image-group-open-source-server-nigos)

NIST describes NBIS as software developed for the FBI and DHS. Its IMGTOOLS
component includes WSQ encoding and decoding.

The following files in the extracted archive establish the decoder's origin:

| Source-relative path | Purpose |
| --- | --- |
| `imgtools\src\bin\dwsq\dwsq.c` | NIST's command-line decoder; names Craig Watson and Michael Garris as authors and carries the NIST license/disclaimer below. |
| `imgtools\src\lib\wsq\decoder.c` | Native WSQ decoding implementation, including `wsq_decode_mem`. |
| `imgtools\src\bin\dwsq\Makefile` | Builds `dwsq` and links its seven NBIS libraries. |
| `man\man1\dwsq.1` | Upstream command-line documentation. |
| `INSTALL_WINDOWS.txt` | Upstream Windows build instructions using MinGW/MSYS. |

The build record reports no changes to NIST's source code or decoding algorithm.
The original downloaded ZIP's SHA-256 was not recorded, so the executable
checksums below identify the retained binaries, not a cryptographically verified
chain back to the downloaded archive.

## Bundled artifacts

Built on **2026-10-05**. The architectures below were checked from the executable
PE headers, rather than inferred from filenames.

| File | Architecture | Size | Build environment | Windows C runtime |
| --- | --- | ---: | --- | --- |
| `dwsq_x32.exe` | x86 / 32-bit (`0x014C`) | 317,024 bytes | MSYS2 MINGW32 | MSVCRT |
| `dwsq_x64.exe` | x64 / 64-bit (`0x8664`) | 292,973 bytes | MSYS2 UCRT64 | Universal CRT (UCRT) |

SHA-256:

```text
dwsq_x32.exe
784A2DCA0FF95AA51078D2E9B3009EE74506EDEAFAC66313BA8D69A2B48C0718

dwsq_x64.exe
E268EB972228F9E8386FBEDC684220DE9F34E312E1652D7C07D4B1B8F2990500
```

Both executables reported the following with `-version`:

```text
Standard Version: ANSI/NIST-ITL 1-2007
NBIS Non-Export Control Software Version: Release 5.0.0
```

The standard-version banner is NBIS version information, not evidence of
certification of these builds or of BiomSharp.

## Build environment and versions

The build used [MSYS2](https://www.msys2.org/), installed at `C:\msys64`.
Versions were recovered on 2026-10-06 from the installed tools and MSYS2 package
installation log, which records the build-day toolchain installations.

| Component | Version / MSYS2 package version |
| --- | --- |
| NBIS source | Release 5.0.0 |
| GCC, both architectures | 16.2.0, MSYS2 Rev4 (`16.2.0-4`) |
| GNU Binutils, both architectures | `2.47-3` |
| GNU Make | 4.4.1 (`4.4.1-3`) |
| CMake (UCRT64 installation) | 4.4.4 (`4.4.4-3`) |
| GNU Bash | 5.3.20(1)-release (`5.3.020-1`) |
| MSYS2 runtime | `3.6.10-6` |
| MinGW-w64 CRT development packages, both architectures | `14.0.0.r426.g4564ee4b5-1` |
| x64 GCC target | `x86_64-w64-mingw32` |
| x86 GCC target | `i686-w64-mingw32` |

MSYS2 is a rolling distribution; its package versions are more informative here
than a single installer version. The CRT package version above is the toolchain
package version, not the version of Windows' runtime DLLs.

**CMake was installed, but was not used to compile this `dwsq` configuration.**
NBIS's optional OpenJPEG build uses CMake; `--STDLIBS` disables OpenJPEG and PNG
support. The decoder and its required libraries were built with GCC and GNU Make
using NBIS's supplied makefiles.

## How the binaries were built

The x64 source was extracted to `C:\GitBucket\nbis\Rel_5.0.0`. A separate clean
extraction at `C:\GitBucket\nbis\Rel_5.0.0-x86` was used for x86, avoiding mixed
32-bit and 64-bit object files or libraries.

The original x64 attempt ran `make it` at the source root. It built the required
libraries, then failed in the unrelated `histogen` utility because modern GCC
rejects its multiple definitions of `histo_head`. Building `dwsq` from its own
source directory then succeeded. No `histogen` source patch or `-fcommon`
workaround was needed. The x86 build used the targeted library/decoder procedure.
An initial missing `fet.h` error was resolved by exporting the required headers.

The following consolidates that procedure for future builds. Run commands one at
a time and stop on errors. Use fresh source extractions and adjust the local
paths as needed. These instructions do not guarantee byte-for-byte identical
binaries; build paths, timestamps and toolchain updates can change the hashes.

### 1. Prepare MSYS2

Update MSYS2, following any terminal-restart instructions:

```bash
pacman -Syu
```

Install the x64 tools in **MSYS2 UCRT64**:

```bash
pacman -S --needed mingw-w64-ucrt-x86_64-gcc make mingw-w64-ucrt-x86_64-cmake
```

Install the x86 compiler:

```bash
pacman -S --needed mingw-w64-i686-gcc make
```

These commands install currently available packages, not necessarily the exact
historical versions listed above.

### 2. Configure one architecture

For **x64**, open MSYS2 UCRT64 (`C:\msys64\ucrt64.exe`):

```bash
echo "$MSYSTEM"
gcc -dumpmachine
cd "$(cygpath -u 'C:\GitBucket\nbis\Rel_5.0.0')"
mkdir -p "$(cygpath -u 'C:\GitBucket\nbis\deploy-dwsq')"
bash setup.sh "$(cygpath -m 'C:\GitBucket\nbis\deploy-dwsq')" --MSYS --64 --STDLIBS
make config
```

The first two commands must report `UCRT64` and `x86_64-w64-mingw32`.

For **x86**, open MSYS2 MINGW32 (`C:\msys64\mingw32.exe`), not UCRT64:

```bash
echo "$MSYSTEM"
gcc -dumpmachine
cd "$(cygpath -u 'C:\GitBucket\nbis\Rel_5.0.0-x86')"
mkdir -p "$(cygpath -u 'C:\GitBucket\nbis\deploy-dwsq')"
bash setup.sh "$(cygpath -m 'C:\GitBucket\nbis\deploy-dwsq')" --MSYS --32 --STDLIBS
make config
```

The first two commands must report `MINGW32` and `i686-w64-mingw32`.
Changing only `--64` to `--32` in UCRT64 is not a substitute for selecting the
32-bit compiler. Run `setup.sh` from the source root; it normally succeeds without
a success message.

### 3. Build the required libraries and decoder

From the configured source root, in the matching shell:

```bash
make -C ijg cpheaders
make -C commonnbis cpheaders
make -C an2k cpheaders
make -C imgtools cpheaders
make -C commonnbis LIBRARYS="fet ioutil util" libs
make -C imgtools libs
make -C "$(cygpath -u 'imgtools\src\bin\dwsq')" it
```

This links `libimage.a`, `libihead.a`, `libwsq.a`, `libjpegl.a`, `libfet.a`,
`libioutil.a`, and `libutil.a`, plus `-lm`. The recorded compiler flags were:

```text
-O2 -w -ansi -DOPJ_STATIC -D_POSIX_SOURCE -DNO_FORK_AND_EXECL
-D__NBISLE__ -D__MSYS__ -m64
```

The x86 build used `-m32` instead of `-m64`. These are the flags emitted by NBIS's
makefiles, not additional local algorithm or optimization changes.

### 4. Copy with an explicit architecture-specific filename

The output is `imgtools\bin\dwsq.exe` in each source tree. From that tree's root,
copy the matching build:

```bash
# UCRT64 / x64 source tree:
cp -i "$(cygpath -u 'imgtools\bin\dwsq.exe')" "$(cygpath -u 'C:\GitBucket\nbis\deploy-dwsq\dwsq_x64.exe')"

# MINGW32 / x86 source tree:
cp -i "$(cygpath -u 'imgtools\bin\dwsq.exe')" "$(cygpath -u 'C:\GitBucket\nbis\deploy-dwsq\dwsq_x32.exe')"
```

Run only the copy command matching the current tree. `-i` prompts before
overwriting. Explicit copying avoids the legacy `make install` destination
ambiguity encountered during the original build, where a missing destination
directory resulted in a file named `bin.exe`.

The retained files were placed in this repository under their final names
`dwsq_x32.exe` and `dwsq_x64.exe`.

## Runtime use and integrity

These are native Windows executables. MSYS2, GCC, Make and CMake are build tools;
they are not required to run the bundled decoders. Inspection of their PE import
tables found Windows DLL imports only: `KERNEL32.dll` and `msvcrt.dll` for x86,
and `KERNEL32.dll` plus Universal CRT API-set DLLs for x64. Neither imports
`msys-2.0.dll` or a separate GCC runtime DLL. The x64 build requires the Windows
Universal CRT to be available.

From this directory in PowerShell:

```powershell
.\dwsq_x32.exe -version
.\dwsq_x64.exe -version
Get-FileHash -Algorithm SHA256 -LiteralPath '.\dwsq_x32.exe', '.\dwsq_x64.exe'
```

For manual decoding, use a disposable working copy of the input:

```powershell
.\dwsq_x64.exe raw 'C:\Temp\nbis-check\input.wsq' -raw_out
```

This writes `input.raw` (decoded grayscale pixels) and `input.ncm` (NISTCOM
metadata) alongside the input, not a PGM file. Avoid running it directly against
original test datasets.

`Models\DecodeWsqFile.cs` selects the executable using `Environment.Is64BitProcess`
and checks its SHA-256 before execution. The test project copies the executables
to its output under `NativeTools\NBIS\5.0.0`; the wrapper runs each decode on an
isolated input copy and reads the dimensions and resolution from NISTCOM.

When replacing these binaries, retain the source archive and its SHA-256, record
toolchain/package versions and any source patches, check PE architecture and DLL
imports, and update both this document and the hashes in `DecodeWsqFile.cs`.
Do not change a hash merely to bypass a verification failure.

Comparison with this decoder is a reference-implementation comparison, **not
official NIST/FBI certification or endorsement**. WSQ is lossy: decoded pixels
need not equal the original uncompressed image, and compiler/architecture
differences can affect numerical results.

## NIST license and disclaimer

The following notice is reproduced from NBIS 5.0.0's `dwsq.c`. It applies to the
NIST software; it does not replace any applicable third-party/toolchain notices.
See also [NIST's NIGOS license agreement and disclaimer](https://www.nist.gov/document/nigoslicdis061906pdf).

```text
License:
This software and/or related materials was developed at the National Institute
of Standards and Technology (NIST) by employees of the Federal Government
in the course of their official duties. Pursuant to title 17 Section 105
of the United States Code, this software is not subject to copyright
protection and is in the public domain.

This software and/or related materials have been determined to be not subject
to the EAR (see Part 734.3 of the EAR for exact details) because it is
a publicly available technology and software, and is freely distributed
to any interested party with no licensing requirements. Therefore, it is
permissible to distribute this software as a free download from the internet.

Disclaimer:
This software and/or related materials was developed to promote biometric
standards and biometric technology testing for the Federal Government
in accordance with the USA PATRIOT Act and the Enhanced Border Security
and Visa Entry Reform Act. Specific hardware and software products identified
in this software were used in order to perform the software development.
In no case does such identification imply recommendation or endorsement
by the National Institute of Standards and Technology, nor does it imply that
the products and equipment identified are necessarily the best available
for the purpose.

This software and/or related materials are provided "AS-IS" without warranty
of any kind including NO WARRANTY OF PERFORMANCE, MERCHANTABILITY,
NO WARRANTY OF NON-INFRINGEMENT OF ANY 3RD PARTY INTELLECTUAL PROPERTY
or FITNESS FOR A PARTICULAR PURPOSE or for any purpose whatsoever, for the
licensed product, however used. In no event shall NIST be liable for any
damages and/or costs, including but not limited to incidental or consequential
damages of any kind, including economic damage or injury to property and lost
profits, regardless of whether NIST shall be advised, have reason to know,
or in fact shall know of the possibility.

By using this software, you agree to bear all risk relating to quality,
use and performance of the software and/or related materials. You agree
to hold the Government harmless from any claim arising from your use
of the software.
```
