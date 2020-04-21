// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here

#include <msclr/lock.h>
#include <msclr/marshal.h>

#pragma unmanaged

#define __inline__ inline
#define __builtin_expect(expr, val) expr


#include <EXTERN.h>               /* from the Perl distribution     */
#include <perl.h>                 /* from the Perl distribution     */

#pragma comment(lib, "Perl5-min\\perl\\lib\\CORE\\libperl530.a")

#pragma managed

#endif //PCH_H
