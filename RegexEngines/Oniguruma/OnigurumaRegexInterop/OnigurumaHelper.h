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


#define DECLARE_IS_OP( flag ) \
		property bool Is##flag \
		{  \
			bool get() \
			{ \
				return (mSyntax->op & flag) != 0; \
			} \
		}

		//DECLARE_IS_OP( ONIG_SYN_OP_ESC_ASTERISK_ZERO_INF )
		//DECLARE_IS_OP( ONIG_SYN_OP_ESC_PLUS_ONE_INF )
		//DECLARE_IS_OP( ONIG_SYN_OP_ESC_QMARK_ZERO_ONE )
		DECLARE_IS_OP( ONIG_SYN_OP_BRACE_INTERVAL )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_BRACE_INTERVAL )
		//DECLARE_IS_OP( ONIG_SYN_OP_ESC_VBAR_ALT )
		DECLARE_IS_OP( ONIG_SYN_OP_LPAREN_SUBEXP )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_LPAREN_SUBEXP )
		//DECLARE_IS_OP( ONIG_SYN_OP_ESC_LTGT_WORD_BEGIN_END )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_C_CONTROL )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_OCTAL3 )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_X_HEX2 )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_X_BRACE_HEX8 )
		DECLARE_IS_OP( ONIG_SYN_OP_ESC_O_BRACE_OCTAL )
		DECLARE_IS_OP( ONIG_SYN_OP_BRACKET_CC )
		DECLARE_IS_OP( ONIG_SYN_OP_POSIX_BRACKET )

#undef DECLARE_IS_OP


#define DECLARE_IS_OP2( flag ) \
		property bool Is##flag \
		{  \
			bool get() \
			{ \
				return (mSyntax->op2 & flag) != 0; \
			} \
		}

		DECLARE_IS_OP2( ONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE )
		DECLARE_IS_OP2( ONIG_SYN_OP2_QMARK_GROUP_EFFECT )
		DECLARE_IS_OP2( ONIG_SYN_OP2_CCLASS_SET_OP )
		DECLARE_IS_OP2( ONIG_SYN_OP2_QMARK_LT_NAMED_GROUP )
		DECLARE_IS_OP2( ONIG_SYN_OP2_ESC_K_NAMED_BACKREF )
		DECLARE_IS_OP2( ONIG_SYN_OP2_ESC_G_SUBEXP_CALL )
		DECLARE_IS_OP2( ONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY )
		DECLARE_IS_OP2( ONIG_SYN_OP2_ESC_P_BRACE_CHAR_PROPERTY )
		DECLARE_IS_OP2( ONIG_SYN_OP2_ESC_P_BRACE_CIRCUMFLEX_NOT )

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

