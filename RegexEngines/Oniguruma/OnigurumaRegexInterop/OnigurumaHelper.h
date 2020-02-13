#pragma once


using namespace System;


namespace OnigurumaRegexInterop
{

	public ref class OnigurumaHelper
	{
	internal:
		OnigurumaHelper( String^ syntaxName, OnigSyntaxType& syntax, OnigOptionType compileOptions );

		!OnigurumaHelper( );
		~OnigurumaHelper( );

		OnigSyntaxType* GetSyntax( ) { return mSyntax; }
		OnigOptionType GetCompileOptions( ) { return mCompileOptions; }

	public:

		String^ GetKey( );

		//#define DECLARE_IS_SYNTAX_PROP(syntax) \
		//		property bool Is##syntax \
		//		{  \
		//			bool get() \
		//			{ \
		//				return memcpy(syntax, mSyntax, sizeof(OnigSyntaxType)) == 0; \
		//			} \
		//		}


#define DECLARE_IS_SYNTAX_PROP( syntax ) \
		property bool Is##syntax \
		{  \
			bool get() \
			{ \
				return mSyntaxName == L#syntax; \
			} \
		}

		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_ASIS )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_POSIX_BASIC )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_POSIX_EXTENDED )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_EMACS )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_GREP )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_GNU_REGEX )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_JAVA )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_PERL )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_PERL_NG )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_RUBY )
		DECLARE_IS_SYNTAX_PROP( ONIG_SYNTAX_ONIGURUMA )

#undef DECLARE_IS_SYNTAX_PROP

#define DECLARE_IS_OP2( flag ) \
		property bool Is##flag \
		{  \
			bool get() \
			{ \
				return (mSyntax->op2 & flag) != 0; \
			} \
		}

		DECLARE_IS_OP2( ONIG_SYN_OP2_QMARK_GROUP_EFFECT )

#undef DECLARE_IS_OP2


#define DECLARE_IS_COMPILE_OPTION( opt ) \
		property bool Is##opt \
		{  \
			bool get() \
			{ \
				return (mCompileOptions & opt) != 0; \
			} \
		}

		DECLARE_IS_COMPILE_OPTION( ONIG_OPTION_EXTEND )

#undef DECLARE_IS_COMPILE_OPTION

	private:

		String^ mSyntaxName;
		OnigSyntaxType* mSyntax;
		OnigOptionType const mCompileOptions;
	};

}

