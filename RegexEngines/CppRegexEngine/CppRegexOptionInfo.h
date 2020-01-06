#pragma once

using namespace System;

using namespace RegexEngineInfrastructure;


namespace CppRegexEngine
{
	ref class CppRegexOptionInfo : public IRegexOptionInfo
	{
	public:

		CppRegexOptionInfo( String^ text, String^ note, String^ asText, std::wregex::flag_type flag )
			: mText( text ), mNote( note ), mAsText( asText ), mFlag( flag)
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
	};
}


