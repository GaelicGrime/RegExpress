// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <strsafe.h>

#include <cassert>
#include <string>


#include "unicode/regex.h"

#pragma comment(lib, "ICU-min\\lib64\\icuin.lib")
//#pragma comment(lib, "ICU-min\\lib64\\icuio.lib")
//#pragma comment(lib, "ICU-min\\lib64\\icutu.lib")
#pragma comment(lib, "ICU-min\\lib64\\icuuc.lib")



#endif //PCH_H
