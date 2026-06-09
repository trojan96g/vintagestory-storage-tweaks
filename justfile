download-deps:
    #!/usr/bin/env bash
    OVERHAULLIB_VER=1.21.0
    mkdir -p lib/overhaullib
    cd lib/overhaullib
    curl -LO https://mods.vintagestory.at/download/78190/overhaullib_$OVERHAULLIB_VER.zip
    unzip overhaullib_$OVERHAULLIB_VER.zip
    rm overhaullib_$OVERHAULLIB_VER.zip
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
