#pragma once

using namespace System;

using namespace RegexEngineInfrastructure;


namespace CppRegexEngine
{
	ref class CppRegexOptionInfo : public RegexOptionInfo
	{
	public:

		CppRegexOptionInfo( String^ text, String^ note, String^ asText, std::wregex::flag_type flag )
			: mText( text ), mNote( note ), mAsText( asText ), mFlag( flag)
		{

		}

#pragma region RegexOptionInfo

		property String^ Text
		{
			String^ get( ) override
			{
				return mText;
			}
		}

		property String^ Note
		{
			String^ get( ) override
			{
				return mNote;
			}
		}

		property String^ AsText
		{
			String^ get( ) override
			{
				return mAsText;
			}
		}

#pragma endregion


		property std::wregex::flag_type Flag
		{
			std::wregex::flag_type get( )
			{
				return mFlag;
			}
		}


	private:

		String^ const mText;
		String^ const mNote;
		String^ const mAsText;
		std::wregex::flag_type const mFlag;

		std::wregex::flag_type mNativeFlag;
	};
}


