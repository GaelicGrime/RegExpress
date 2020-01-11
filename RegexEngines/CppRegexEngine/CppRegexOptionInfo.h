#pragma once

using namespace System;

using namespace RegexEngineInfrastructure;


namespace CppRegexEngine
{
	ref class CppRegexSimpleOptionInfo : public IRegexSimpleOptionInfo
	{
	public:

		CppRegexSimpleOptionInfo( String^ text, String^ note, String^ asText, std::wregex::flag_type flag )
			: mText( text ), mNote( note ), mAsText( asText ), mFlag( flag )
		{

		}


#pragma region IRegexOptionInfo

		virtual property String^ Text
		{
			String^ get( )
			{
				return mText;
			}
		}

#pragma endregion IRegexOptionInfo


#pragma region IRegexSimpleOptionInfo

		virtual property String^ Note
		{
			String^ get( )
			{
				return mNote;
			}
		}

		virtual property String^ AsText
		{
			String^ get( )
			{
				return mAsText;
			}
		}

#pragma endregion IRegexSimpleOptionInfo


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
	};
}


