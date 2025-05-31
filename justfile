game := "C:/Users/Jakob/Documents/dev/contrib/RustyAssetBundleEXtractor/out"

examplemod:
    uvx unity-scene-repacker \
        --steam-game "Hollow Knight" \
        --objects Resources/bundle.objects.jsonc \
        --output Resources/bundle.unity3d --bundle-name examplemod
