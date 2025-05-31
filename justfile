game := "C:/Program Files (x86)/Steam/steamapps/common/Nine Sols-Speedrunpatch/NineSols_Data"

examplemod:
    uvx unity-scene-repacker \
        --game-dir "{{game}}" \
        --objects Resources/bundle.objects.json \
        --output Resources/bundle.unity3d
