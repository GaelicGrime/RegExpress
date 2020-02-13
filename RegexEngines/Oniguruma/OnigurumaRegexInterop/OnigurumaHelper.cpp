#include "pch.h"
#include "OnigurumaHelper.h"


namespace OnigurumaRegexInterop
{

	OnigurumaHelper::OnigurumaHelper( String^ syntaxName, OnigSyntaxType& syntax, OnigOptionType compileOptions )
		:
		mSyntaxName( syntaxName ),
		mSyntax( nullptr ),
		mCompileOptions( compileOptions )
	{
		mSyntax = new OnigSyntaxType{};

		onig_copy_syntax( mSyntax, &syntax );
	}


	OnigurumaHelper::~OnigurumaHelper( )
	{
		this->!OnigurumaHelper( );
	}


	OnigurumaHelper::!OnigurumaHelper( )
	{
		delete mSyntax;
		mSyntax = nullptr;
	}


	String^ OnigurumaHelper::GetKey( )
	{
		return String::Join( "\u001F", mSyntax->op, mSyntax->op2, mSyntax->behavior, mSyntax->options, mCompileOptions );
	}

}