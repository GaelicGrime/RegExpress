#include "pch.h"

#include "CppRegexEngine.h"


using namespace std;


namespace CppRegexEngine
{
	void CppRegexEngine::Matches( String^ text0, String^ pattern0, CppRegexOptions options0 )
	{
		msclr::interop::marshal_context context{};

		wstring text = context.marshal_as<wstring>( text0 );
		wstring pattern = context.marshal_as<wstring>( pattern0 );

		wregex::flag_type flags = static_cast<wregex::flag_type>( options0 );

		wregex re( pattern, flags );

		auto results_begin = wsregex_iterator( text.cbegin( ), text.cend( ), re );
		auto results_end = std::wsregex_iterator( );

		for( auto i = results_begin; i != results_end; ++i )
		{
			const wsmatch& match = *i;

			auto submatches_begin = match.cbegin( );
			auto submatches_end = match.cend( );

			for( auto j = submatches_begin; j != submatches_end; ++j )
			{
				wssub_match submatch = *j;

				//...

			}

		}


	}
}
