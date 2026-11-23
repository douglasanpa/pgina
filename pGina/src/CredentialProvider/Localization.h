#pragma once

#include <Windows.h>

namespace pGina
{
	namespace Localization
	{
		// Returns the text in the configured language (registry "Language" value, or the
		// Windows display language when it is empty). The English text is the key; text
		// without a translation is returned as is.
		const wchar_t * T(const wchar_t *english);
	}
}
