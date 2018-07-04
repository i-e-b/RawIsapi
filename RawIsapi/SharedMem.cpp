#include "stdafx.h"
#include "SharedMem.h"

HANDLE      hMappedFileObject = NULL;  // handle to mapped file
LPVOID      lpvSharedMem = NULL;       // pointer to shared memory
const int   SHARED_MEM_SIZE = sizeof(ULONGLONG);

BOOL CreateSharedMem()
{
    // Create a named file mapping object
    hMappedFileObject = CreateFileMapping(
        INVALID_HANDLE_VALUE,
        NULL,
        PAGE_READWRITE,
        0,
        SHARED_MEM_SIZE,
        TEXT("shmemfile") // Name of shared mem file
    );

    if (hMappedFileObject == NULL)
    {
        return FALSE;
    }

    BOOL bFirstInit = (ERROR_ALREADY_EXISTS != GetLastError());

    // Get a ptr to the shared memory
    lpvSharedMem = MapViewOfFile(hMappedFileObject, FILE_MAP_WRITE, 0, 0, 0);

    if (lpvSharedMem == NULL)
    {
        return FALSE;
    }

    if (bFirstInit) // First time the shared memory is accessed?
    {
        ZeroMemory(lpvSharedMem, SHARED_MEM_SIZE);
    }

    return TRUE;
}

BOOL SetSharedMem(ULONGLONG _64bitValue)
{
    BOOL bOK = CreateSharedMem();

    if (bOK)
    {
        ULONGLONG* pSharedMem = (ULONGLONG*)lpvSharedMem;
        *pSharedMem = _64bitValue;
    }

    return bOK;
}

BOOL GetSharedMem(ULONGLONG* p64bitValue)
{
    if (p64bitValue == NULL) return FALSE;

    BOOL bOK = CreateSharedMem();

    if (bOK)
    {
        ULONGLONG* pSharedMem = (ULONGLONG*)lpvSharedMem;
        *p64bitValue = *pSharedMem;
    }

    return bOK;
}