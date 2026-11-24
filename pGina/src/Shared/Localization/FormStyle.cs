/*
	Copyright (c) 2026, pGina Team
	All rights reserved.

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions are met:
		* Redistributions of source code must retain the above copyright
		  notice, this list of conditions and the following disclaimer.
		* Redistributions in binary form must reproduce the above copyright
		  notice, this list of conditions and the following disclaimer in the
		  documentation and/or other materials provided with the distribution.
		* Neither the name of the pGina Team nor the names of its contributors
		  may be used to endorse or promote products derived from this software without
		  specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
	ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
	DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
	(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
	LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
	ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace pGina.Shared.Localization
{
    /// <summary>
    /// Common look of the configuration windows (pGina and its plugins). Call it in the form
    /// constructor, right after InitializeComponent.
    /// </summary>
    public static class FormStyle
    {
        public static readonly string FontName = "Segoe UI";
        public const float FontSize = 9F;

        public static void Apply(Form form)
        {
            // with AutoScaleMode.Font the form scales its layout to the new font
            Font font = new Font(FontName, FontSize, FontStyle.Regular, GraphicsUnit.Point);
            if (font.Name == FontName)
                ReplaceFonts(form, form.Font, font);

            Language.Apply(form);
        }

        // Controls with their own font in the designer (bold or italic labels, grid styles) keep
        // their style, with the new font family.
        private static void ReplaceFonts(Control c, Font oldDefault, Font newDefault)
        {
            foreach (Control child in c.Controls)
            {
                if (!child.Font.Equals(oldDefault) && child.Font.Name != newDefault.Name && child.Font.FontFamily.Name.Contains("Sans Serif"))
                    child.Font = new Font(newDefault.FontFamily, newDefault.Size, child.Font.Style, GraphicsUnit.Point);
                ReplaceFonts(child, oldDefault, newDefault);
            }
            if (c is Form)
                c.Font = newDefault;
        }
    }
}
