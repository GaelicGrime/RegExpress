/*
* Based on: "Get started with WebView2", https://docs.microsoft.com/en-us/microsoft-edge/webview2/get-started/win32
*
*/

// compile with: /D_UNICODE /DUNICODE /DWIN32 /D_WINDOWS /c

#include "pch.h"

using namespace Microsoft::WRL;

// Global variables

// The main window class name.
static TCHAR szWindowClass[] = _T( "RegExpressWebViewClient" );

// The string that appears in the application's title bar.
static TCHAR szTitle[] = _T( "RegExpressWebViewClient" );

HINSTANCE hInst;

// Forward declarations of functions included in this code module:
LRESULT CALLBACK WndProc( HWND, UINT, WPARAM, LPARAM );

// Pointer to WebViewController
static wil::com_ptr<ICoreWebView2Controller> webviewController;

// Pointer to WebView window
static wil::com_ptr<ICoreWebView2> webviewWindow;


int DoGetVersion( );
int DoMatch( HINSTANCE hInstance, LPCWSTR pattern, LPCWSTR flags, LPCWSTR text );


int APIENTRY WinMain(
	HINSTANCE hInstance,
	HINSTANCE hPrevInstance,
	LPSTR     lpCmdLine,
	int       nCmdShow
)
{
	//AttachConsole( ATTACH_PARENT_PROCESS ); // (does not seem to have effect)

	HRESULT hr = CoInitializeEx( nullptr, COINIT_APARTMENTTHREADED );

	if( hr != S_OK && hr != S_FALSE )
	{
		std::cerr << "CoInitializeEx failed" << std::endl;

		return 1;
	}

	LPCWSTR command_line = GetCommandLineW( );
	int argc = 0;

	LPWSTR* argv = CommandLineToArgvW( command_line, &argc );

	if( argv == NULL )
	{
		std::wcerr << L"Failed to parse command line: '" << command_line << "'." << std::endl;

		return 1;
	}

	if( argc < 2 )
	{
		std::wcerr << L"Invalid command line: '" << command_line << "'." << std::endl;

		return 1;
	}


	if( lstrcmpiW( argv[1], L"v" ) == 0 ) // "v" -- get version
	{
		return DoGetVersion( );
	}


	std::wstring stdin_contents;

	if( lstrcmpiW( argv[1], L"i" ) == 0 ) // "i" -- get data from STDIN instead of command-line arguments
	{
		std::getline( std::wcin, stdin_contents, L'\r' );

		stdin_contents = L"\"" + ( argv[0] + ( L"\" " + stdin_contents ) );

		command_line = stdin_contents.c_str( );
		argv = CommandLineToArgvW( command_line, &argc );
	}

	if( lstrcmpiW( argv[1], L"m" ) == 0 ) // "m" -- get matches
	{
		if( argc < 5 )
		{
			std::wcerr << L"Invalid command line: '" << command_line << "'." << std::endl;

			return 1;
		}

		return DoMatch( hInst, argv[2], argv[3], argv[4] );
	}

	if( lstrcmpiW( argv[1], L"t" ) == 0 ) // "t" -- test things
	{
		std::wcerr << L"Command line: '" << command_line << "'" << std::endl;

		for( int i = 0; i < argc; ++i )
		{
			std::wcerr << i << ": '" << argv[i] << "'" << std::endl;
		}

		return 0;
	}

	std::wcerr << L"Invalid command line: '" << command_line << "'." << std::endl;

	return 1;
}


int DoGetVersion( )
{
	PWSTR v;
	HRESULT hr = GetAvailableCoreWebView2BrowserVersionString( NULL, &v );

	if( hr != S_OK )
	{
		std::wcerr << L"Failed to get version" << std::endl;

		return 1;
	}

	std::wcout << L"{\"v\": \"" << v << "\" }" << std::endl;

	return 0;
}


int DoMatch( HINSTANCE hInstance, LPCWSTR pattern, LPCWSTR flags, LPCWSTR text )
{
	int exit_code = 0;

	WNDCLASSEX wcex = { 0 };

	wcex.cbSize = sizeof( WNDCLASSEX );
	wcex.style = CS_HREDRAW | CS_VREDRAW;
	wcex.lpfnWndProc = WndProc;
	//wcex.cbClsExtra = 0;
	//wcex.cbWndExtra = 0;
	wcex.hInstance = hInstance;
	//wcex.hIcon = NULL;
	//wcex.hCursor = LoadCursor( NULL, IDC_ARROW );
	wcex.hbrBackground = (HBRUSH)( COLOR_WINDOW + 1 );
	//wcex.lpszMenuName = NULL;
	wcex.lpszClassName = szWindowClass;
	//wcex.hIconSm = NULL;

	if( !RegisterClassEx( &wcex ) )
	{
		std::cerr << "RegisterClassEx failed" << std::endl;
		exit_code = 1;

		return exit_code;
	}

	// Store instance handle in our global variable
	hInst = hInstance;

	// The parameters to CreateWindow explained:
	// szWindowClass: the name of the application
	// szTitle: the text that appears in the title bar
	// WS_OVERLAPPEDWINDOW: the type of window to create
	// CW_USEDEFAULT, CW_USEDEFAULT: initial position (x, y)
	// 500, 100: initial size (width, length)
	// NULL: the parent of this window
	// NULL: this application does not have a menu bar
	// hInstance: the first parameter from WinMain
	// NULL: not used in this application
	HWND hWnd = CreateWindow(
		szWindowClass,
		szTitle,
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, CW_USEDEFAULT,
		1200, 900,
		NULL,
		NULL,
		hInstance,
		NULL
	);

	if( !hWnd )
	{
		std::cerr << "CreateWindow failed" << std::endl;
		exit_code = 1;

		return exit_code;
	}

	// The parameters to ShowWindow explained:
	// hWnd: the value returned from CreateWindow
	// nCmdShow: the fourth parameter from WinMain
	ShowWindow( hWnd, SW_HIDE );
	//UpdateWindow( hWnd );


	// <-- WebView2 sample code starts here -->

	// Step 3 - Create a single WebView within the parent window
	// Locate the browser and set up the environment for WebView
	CreateCoreWebView2EnvironmentWithOptions( nullptr, nullptr, nullptr,
		Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
			[hWnd, pattern, flags, text, &exit_code]( HRESULT result, ICoreWebView2Environment* env ) -> HRESULT
			{
				if( result != S_OK )
				{
					std::cerr << "CreateCoreWebView2EnvironmentWithOptions failed" << std::endl;
					exit_code = 1;
					DestroyWindow( hWnd );

					return S_FALSE;
				}

				if( !env )
				{
					std::cerr << "env is null" << std::endl;
					exit_code = 1;
					DestroyWindow( hWnd );

					return S_FALSE;
				}

				// Create a CoreWebView2Controller and get the associated CoreWebView2 whose parent is the main window hWnd
				env->CreateCoreWebView2Controller( hWnd, Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
					[hWnd, pattern, flags, text, &exit_code]( HRESULT result, ICoreWebView2Controller* controller ) -> HRESULT
					{
						if( result != S_OK )
						{
							std::cerr << "CreateCoreWebView2Controller failed" << std::endl;
							exit_code = 1;
							DestroyWindow( hWnd );

							return S_FALSE;
						}

						if( !controller )
						{
							std::cerr << "controller is null" << std::endl;
							exit_code = 1;
							DestroyWindow( hWnd );

							return S_FALSE;
						}

						webviewController = controller;
						webviewController->get_CoreWebView2( &webviewWindow );

						// Add a few settings for the webview
						// The demo step is redundant since the values are the default settings
						ICoreWebView2Settings* Settings;
						webviewWindow->get_Settings( &Settings );
						Settings->put_IsScriptEnabled( TRUE );
						Settings->put_AreDefaultScriptDialogsEnabled( FALSE );
						Settings->put_IsWebMessageEnabled( FALSE );

						// Resize WebView to fit the bounds of the parent window
						RECT bounds;
						GetClientRect( hWnd, &bounds );
						webviewController->put_Bounds( bounds );

						//// Schedule an async task to navigate to Bing
						//webviewWindow->Navigate( L"https://www.bing.com/" );
						//webviewWindow->Navigate( L"about:blank" );

						// Step 4 - Navigation events

						// Step 5 - Scripting

						// Schedule an async task to add initialization script that freezes the Object object
						//webviewWindow->AddScriptToExecuteOnDocumentCreated( L"Object.freeze(Object);", nullptr );
						//webviewWindow->ExecuteScript( L"alert('Hello, JavaScript!')", nullptr );
						//// Schedule an async task to get the document URL
						//webviewWindow->ExecuteScript( L"window.document.URL;", Callback<ICoreWebView2ExecuteScriptCompletedHandler>(
						//	[]( HRESULT errorCode, LPCWSTR resultObjectAsJson ) -> HRESULT {
						//		LPCWSTR URL = resultObjectAsJson;
						//		//doSomethingWithURL(URL);
						//		return S_OK;
						//	} ).Get( ) );


						std::wstring flags_adjusted = std::wstring( flags ) + L"gd";

#define EOL L"\r\n"

						std::wstring script =
							std::wstring( L"( function() " EOL
								"{ " EOL ) +
							L"let re = new RegExp(\"" + pattern + L"\", \"" + flags_adjusted + L"\"); " EOL
							L"let r = [ ]; let m; " EOL
							L"while( (m = re.exec(\"" + text + L"\")) != null) " EOL
							L"{ " EOL
							L"   r.push( { i: m.indices, g: m.indices.groups } );" EOL
							L"} " EOL
							L"return r; " EOL
							L"} )()";


						webviewWindow->ExecuteScript( script.c_str( ),
							Callback<ICoreWebView2ExecuteScriptCompletedHandler>(
								[hWnd, &exit_code]( HRESULT errorCode, LPCWSTR resultObjectAsJson ) -> HRESULT
								{
									if( errorCode != S_OK )
									{
										std::cerr << "JavaScript failed" << std::endl;

										exit_code = 1;
										DestroyWindow( hWnd );

										return S_FALSE;
									}

									LPCWSTR json = resultObjectAsJson;
									//MessageBox( hWnd, json, L"Result", MB_OKCANCEL );

									std::wcout << json << std::endl;

									DestroyWindow( hWnd );

									return S_OK;
								} ).Get( ) );

						// Step 6 - Communication between host and web content

						return S_OK;
					} ).Get( ) );
				return S_OK;
			} ).Get( ) );

	// <-- WebView2 sample code ends here -->

	// Main message loop:
	MSG msg;
	while( GetMessage( &msg, NULL, 0, 0 ) )
	{
		TranslateMessage( &msg );
		DispatchMessage( &msg );
	}

	return exit_code;
}


//  FUNCTION: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  PURPOSE:  Processes messages for the main window.
//
//  WM_DESTROY  - post a quit message and return
LRESULT CALLBACK WndProc( HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam )
{
	switch( message )
	{
	case WM_SIZE:
		if( webviewController != nullptr )
		{
			RECT bounds;
			GetClientRect( hWnd, &bounds );
			webviewController->put_Bounds( bounds );
		};
		break;
	case WM_DESTROY:
		PostQuitMessage( 0 );
		break;
	default:
		return DefWindowProc( hWnd, message, wParam, lParam );
		break;
	}

	return 0;
}
