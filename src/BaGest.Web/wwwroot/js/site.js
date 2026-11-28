// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    'use strict';

    var baget = {};
    window.baget = baget;

    /**
     * Copies the provided text to the clipboard.
     * Uses the modern Clipboard API if available, with a fallback for older browsers.
     */
    baget.copyTextToClipboard = function (text, elementToFocus) {
        // 1. Modern Approach (Clipboard API)
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(function () {
                console.log('Copying text command was successful');
                if (elementToFocus) {
                    elementToFocus.focus();
                }
            }).catch(function (err) {
                console.error('Failed to copy text: ', err);
            });
        } 
        // 2. Fallback Approach (for older browsers or non-secure contexts)
        else {
            var textArea = document.createElement("textarea");
            textArea.value = text;
            
            // Hide the textarea off-screen
            textArea.style.position = "fixed";
            textArea.style.top = "-9999px";
            
            document.body.appendChild(textArea);
            textArea.select();

            try {
                var successful = document.execCommand('copy');
                var msg = successful ? 'successful' : 'unsuccessful';
                console.log('Copying text command was ' + msg);
            } catch (err) {
                console.error('Oops, unable to copy');
            }

            document.body.removeChild(textArea);

            if (elementToFocus) {
                elementToFocus.focus();
            }
        }
    };
})();