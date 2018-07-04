#pragma once

#ifdef SHAREDMEM_EXPORTS
#define SHAREDMEM_API __declspec(dllexport)
#else
#define SHAREDMEM_API __declspec(dllimport)
#endif

#define SHAREDMEM_CALLING_CONV __cdecl

extern "C" {
    SHAREDMEM_API BOOL SHAREDMEM_CALLING_CONV SetSharedMem(ULONGLONG _64bitValue);
    SHAREDMEM_API BOOL SHAREDMEM_CALLING_CONV GetSharedMem(ULONGLONG* p64bitValue);
}