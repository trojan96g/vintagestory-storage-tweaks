download-deps:
    #!/usr/bin/env bash
    mkdir -p lib/configlib
    cd lib/configlib
    curl -Lo configlib.zip "https://mods.vintagestory.at/download/73792/configlib_1.10.14.zip"
    unzip configlib.zip
    rm configlib.zip
    cd -

download-vs-1_22:
    #!/usr/bin/env bash
    VS_DOWNLOAD_URL="https://cdn.vintagestory.at/gamefiles/stable/vs_client_linux-x64_1.22.3.tar.gz"
    curl -o vs_1.22.tar.gz $VS_DOWNLOAD_URL
    if [[ -d "vintagestory_1.22" ]]; then
      rm -r vintagestory_1.22 
    fi
    tar -xzf vs_1.22.tar.gz --one-top-level="vintagestory_1.22" --strip-components 1 

download-vs-1_21:
    #!/usr/bin/env bash
    VS_DOWNLOAD_URL="https://cdn.vintagestory.at/gamefiles/stable/vs_client_linux-x64_1.21.6.tar.gz"
    curl -o vs_1.21.tar.gz $VS_DOWNLOAD_URL
    if [[ -d "vintagestory" ]]; then
      rm -r vintagestory
    fi
    tar -xzf vs_1.21.tar.gz
    test -d vintagestory
