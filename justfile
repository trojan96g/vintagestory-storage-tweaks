download-deps:
    #!/usr/bin/env bash
    OVERHAULLIB_VER=1.21.0
    mkdir -p lib/overhaullib
    cd lib/overhaullib
    curl -LO https://mods.vintagestory.at/download/78190/overhaullib_$OVERHAULLIB_VER.zip
    unzip overhaullib_$OVERHAULLIB_VER.zip
    rm overhaullib_$OVERHAULLIB_VER.zip
    cd -
