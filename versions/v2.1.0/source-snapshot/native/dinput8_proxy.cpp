#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <stdlib.h>
#include <string>
#include <vector>

namespace
{
    HMODULE g_realDinput8 = NULL;
    INIT_ONCE g_loadOnce = INIT_ONCE_STATIC_INIT;

    BOOL CALLBACK LoadRealDinput8(PINIT_ONCE, PVOID, PVOID*)
    {
        wchar_t systemDirectory[MAX_PATH] = { 0 };
        UINT length = GetSystemDirectoryW(systemDirectory, MAX_PATH);
        if (length == 0 || length >= MAX_PATH) return FALSE;
        std::wstring path(systemDirectory);
        path += L"\\dinput8.dll";
        g_realDinput8 = LoadLibraryW(path.c_str());
        return g_realDinput8 != NULL;
    }

    FARPROC RealExport(const char* name)
    {
        InitOnceExecuteOnce(&g_loadOnce, LoadRealDinput8, NULL, NULL);
        return g_realDinput8 == NULL ? NULL : GetProcAddress(g_realDinput8, name);
    }

    std::wstring ModuleDirectory()
    {
        wchar_t modulePath[MAX_PATH] = { 0 };
        HMODULE self = NULL;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&ModuleDirectory), &self);
        DWORD length = GetModuleFileNameW(self, modulePath, MAX_PATH);
        if (length == 0 || length >= MAX_PATH) return std::wstring();
        std::wstring path(modulePath, length);
        std::wstring::size_type separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
    }

    std::wstring ProcessBaseName()
    {
        wchar_t processPath[MAX_PATH] = { 0 };
        DWORD length = GetModuleFileNameW(NULL, processPath, MAX_PATH);
        if (length == 0 || length >= MAX_PATH) return std::wstring();
        std::wstring path(processPath, length);
        std::wstring::size_type separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? path : path.substr(separator + 1);
    }

    bool EnvironmentFlag(const wchar_t* name)
    {
        wchar_t value[8] = { 0 };
        DWORD length = GetEnvironmentVariableW(name, value, 8);
        return length > 0 && length < 8 && value[0] == L'1';
    }

    std::wstring Quote(const std::wstring& value)
    {
        std::wstring quoted = L"\"";
        unsigned int backslashes = 0;
        for (std::wstring::const_iterator it = value.begin(); it != value.end(); ++it)
        {
            if (*it == L'\\')
            {
                ++backslashes;
                continue;
            }
            if (*it == L'\"')
                quoted.append(backslashes * 2 + 1, L'\\');
            else
                quoted.append(backslashes, L'\\');
            backslashes = 0;
            quoted += *it;
        }
        quoted.append(backslashes * 2, L'\\');
        quoted += L'\"';
        return quoted;
    }

    DWORD WINAPI BootstrapThread(LPVOID)
    {
        if (_wcsicmp(ProcessBaseName().c_str(), L"Dead Space.exe") != 0) return 0;
        if (EnvironmentFlag(L"DSTL_TEXMOD_CHILD") || EnvironmentFlag(L"DSTL_DISABLE_BOOTSTRAP")) return 0;

        std::wstring gameRoot = ModuleDirectory();
        if (gameRoot.empty()) return 0;
        std::wstring launcher = gameRoot + L"\\DeadSpaceTextureLauncher.exe";
        DWORD attributes = GetFileAttributesW(launcher.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0) return 0;

        wchar_t pid[32] = { 0 };
        _ultow_s(GetCurrentProcessId(), pid, 32, 10);
        std::wstring command = Quote(launcher) + L" --dinput-bootstrap --game-pid " + pid +
            L" --game-root " + Quote(gameRoot);
        std::vector<wchar_t> mutableCommand(command.begin(), command.end());
        mutableCommand.push_back(L'\0');

        STARTUPINFOW startup = { sizeof(startup) };
        PROCESS_INFORMATION process = { 0 };
        BOOL started = CreateProcessW(launcher.c_str(), &mutableCommand[0], NULL, NULL, FALSE, 0,
            NULL, gameRoot.c_str(), &startup, &process);
        if (!started) return 0;
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);

        // The launcher now owns the session and will relaunch Dead Space through TexMod.
        ExitProcess(0);
        return 0;
    }
}

extern "C" HRESULT WINAPI ProxyDirectInput8Create(HINSTANCE instance, DWORD version,
    REFIID interfaceId, LPVOID* output, LPUNKNOWN outer)
{
    typedef HRESULT (WINAPI *Function)(HINSTANCE, DWORD, REFIID, LPVOID*, LPUNKNOWN);
    Function function = reinterpret_cast<Function>(RealExport("DirectInput8Create"));
    return function == NULL ? E_FAIL : function(instance, version, interfaceId, output, outer);
}

extern "C" HRESULT WINAPI ProxyDllCanUnloadNow()
{
    typedef HRESULT (WINAPI *Function)();
    Function function = reinterpret_cast<Function>(RealExport("DllCanUnloadNow"));
    return function == NULL ? S_FALSE : function();
}

extern "C" HRESULT WINAPI ProxyDllGetClassObject(REFCLSID classId, REFIID interfaceId, LPVOID* output)
{
    typedef HRESULT (WINAPI *Function)(REFCLSID, REFIID, LPVOID*);
    Function function = reinterpret_cast<Function>(RealExport("DllGetClassObject"));
    return function == NULL ? CLASS_E_CLASSNOTAVAILABLE : function(classId, interfaceId, output);
}

extern "C" HRESULT WINAPI ProxyDllRegisterServer()
{
    typedef HRESULT (WINAPI *Function)();
    Function function = reinterpret_cast<Function>(RealExport("DllRegisterServer"));
    return function == NULL ? E_FAIL : function();
}

extern "C" HRESULT WINAPI ProxyDllUnregisterServer()
{
    typedef HRESULT (WINAPI *Function)();
    Function function = reinterpret_cast<Function>(RealExport("DllUnregisterServer"));
    return function == NULL ? E_FAIL : function();
}

extern "C" const void* WINAPI ProxyGetdfDIJoystick()
{
    typedef const void* (WINAPI *Function)();
    Function function = reinterpret_cast<Function>(RealExport("GetdfDIJoystick"));
    return function == NULL ? NULL : function();
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        HANDLE thread = CreateThread(NULL, 0, BootstrapThread, NULL, 0, NULL);
        if (thread != NULL) CloseHandle(thread);
    }
    return TRUE;
}
