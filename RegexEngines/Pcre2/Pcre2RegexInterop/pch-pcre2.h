// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_C_H
#define PCH_C_H

// add headers that you want to pre-compile here


// See "NON-AUTOTOOLS-BUILD" files from PCRE2

#define HAVE_CONFIG_H
#define PCRE2_CODE_UNIT_WIDTH 16
//#define SUPPORT_JIT 1
#define PCRE2_EXP_DEFN
#define PCRE2_EXP_DECL
#define SUPPORT_UNICODE

#endif //PCH_C_H
