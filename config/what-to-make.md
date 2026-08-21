what is that = ac# and a .net app of a ide as well . 

app config = 
    1. name = OCIDE
    2. type = AI ide
    3. manyfacataror = BCSdevloper
    4. ui = WPF withthe smart sync of the theme color
app fasality = 
    1. local ai support
    2. languages supports =  
        1. python
        2. javascript
        3. c
        4. c++
        5. c#
        6. php
        7. sql
        8. sqlite3
        9. json
        10. mongodb
        11. html
        12. css
        13. db
        14. xml
        15. xaml
        16. kotlin
        17. java
        18. TypeScript
        19. ps1
        20. cmd
        21. bat
        23. txt
        24. pdf
        25. jpg
        26. jpeg
        27. png
        28. pdf
        29. svg

    3. project baised controll
    4. project or app locking
    5. local web browser and server for testing the dynamic sites
    6. and also all of thes fasalities that provides by visual stucio code as well . 
    7. internal vershon controll support . 
    8. smart error deataktion 
    10. smart ai chatign support 

---

### 100% Offline Architecture & Improvements

**CRITICAL REQUIREMENT**: The entire IDE must run completely offline without any internet connection.

1. **Local AI Integration (Offline)**: All AI features (chat, error detection) must run locally on the user's machine. Use **LLamaSharp** or **ONNX Runtime** to load models (e.g., Llama 3, CodeQwen) directly from the hard drive. Ensure the AI inference is hardware-accelerated to run on the GPU (e.g., utilizing CUDA or DirectML for NVIDIA RTX GPUs) for optimal performance. Ensure these run on a background thread so the UI doesn't freeze.
2. **Text Editor Component**: Build a **100% Custom WPF Text Editor Component** from scratch. This custom component will inherit directly from a base WPF control (like `Control` or `FrameworkElement`), rendering text manually via `DrawingContext` and `FormattedText` to achieve maximum performance and syntax highlighting control without relying on any third-party libraries (no Monaco, no AvalonEdit, no Scintilla).
3. **Language Support Architecture (Offline LSPs)**: Implement the **Language Server Protocol (LSP)**. Instead of downloading language servers on the fly, bundle the required language servers (e.g., OmniSharp, Pyright) directly with your installer or provide offline plugin packages.
4. **Version Control**: Use the **LibGit2Sharp** library for local Git repository management (commits, branching, local history) without needing an external Git server or internet connection.
5. **Local Web Browser & Server**: The `WebView2` control and local testing servers (e.g., Node.js or Kestrel) will run entirely on `localhost`, keeping all dynamic site testing strictly offline.
6. **Extensibility (Plugins)**: Design a plugin architecture that loads from a local directory. Avoid any web-based plugin marketplaces inside the core IDE to maintain the strict offline requirement.
7. **UI/UX Styling**: Use libraries like **WPF-UI** or **ControlzEx** for a modern Windows 11 design. Ensure all themes and UI assets are embedded as local resources.
