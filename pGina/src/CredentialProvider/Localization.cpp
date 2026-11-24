#include "Localization.h"

#include <string>
#include <wchar.h>

#include <Registry.h>

namespace pGina
{
	namespace Localization
	{
		struct Entry
		{
			const wchar_t *english;
			const wchar_t *portuguese;
		};

		// Keep in sync with the texts used in this project
		static const Entry s_portuguese[] =
		{
			{ L"Image", L"Imagem" },
			{ L"Username", L"Usuário" },
			{ L"Password", L"Senha" },
			{ L"Old Password", L"Senha atual" },
			{ L"New Password", L"Nova senha" },
			{ L"Confirm New Password", L"Confirme a nova senha" },
			{ L"Submit", L"Entrar" },
			{ L"Locked", L"Bloqueado" },
			{ L"Status", L"Status" },
			{ L"Service Status: Connected", L"Serviço: conectado" },
			{ L"Service Status: Disconnected", L"Serviço: desconectado" },
			{ L"Your login request failed, because the pGina service is not running!\nOnly useres from the local administrator group are able to login.\n\nPlease contact your system administrator.\nReboot the machine to fix this issue.",
			  L"O logon falhou porque o serviço do pGina não está em execução!\nSomente usuários do grupo de administradores locais podem entrar.\n\nProcure o administrador do sistema.\nReinicie o computador para corrigir o problema." },
			{ L"Your password change request failed, because the pGina service is not running!\n\nPlease contact your system administrator.\nReboot the machine to fix this issue.",
			  L"A troca de senha falhou porque o serviço do pGina não está em execução!\n\nProcure o administrador do sistema.\nReinicie o computador para corrigir o problema." },
			{ L"Processing password change for ", L"Trocando a senha de " },
			{ L"New passwords do not match", L"As novas senhas não conferem" },
			{ L"Your old password does not match", L"A senha atual está incorreta" },
			{ L"Failed to change password, no message from plugins.", L"Não foi possível trocar a senha, e os plugins não informaram o motivo." },
			{ L"pGina: Your password was successfully changed", L"pGina: sua senha foi trocada" },
			{ L"Processing UAC for ", L"Processando o UAC de " },
			{ L"Processing logon for ", L"Fazendo o logon de " },
			{ L"You will be logged in.\nPlease wait ...", L"Entrando.\nAguarde..." },
			{ L"Plugins did not provide a specific error message", L"Os plugins não informaram o motivo do erro" },
			{ L"Process login failed, but a specific error message was not provided", L"O logon falhou, mas o motivo não foi informado" },
			{ L"Error during login", L"Erro no logon" },
			{ L"waiting for the pGina service ...", L"aguardando o serviço do pGina..." },
		};

		static bool UsePortuguese()
		{
			std::wstring name = pGina::Registry::GetString(L"Language", L"");
			if (name.empty())
			{
				LANGID lang = GetUserDefaultUILanguage();
				return PRIMARYLANGID(lang) == LANG_PORTUGUESE;
			}
			return _wcsnicmp(name.c_str(), L"pt", 2) == 0;
		}

		const wchar_t * T(const wchar_t *english)
		{
			// read on every call: the setting can change while LogonUI keeps the DLL loaded
			if (english == NULL || !UsePortuguese())
				return english;

			for (size_t i = 0; i < sizeof(s_portuguese) / sizeof(s_portuguese[0]); i++)
			{
				if (wcscmp(s_portuguese[i].english, english) == 0)
					return s_portuguese[i].portuguese;
			}
			return english;
		}
	}
}
