# Istruzioni per Completare la Release v1.1.0 su GitHub

## ✅ Operazioni Git Completate

- ✅ Commit delle correzioni (timeout HttpClient, limite righe LogForm, scrittura atomica)
- ✅ Merge di `development-MultiAgent` in `master`
- ✅ Tag `v1.1.0` creato e pushato su GitHub
- ✅ Branch `master` e `development-MultiAgent` aggiornati su GitHub

## 📦 Passaggi per Creare la Release su GitHub

### 1. Vai alla pagina Releases

Apri il browser e vai a:
```
https://github.com/mgiane86/ProxyAI/releases/new
```

### 2. Configurazione Release

**Choose a tag:** Seleziona `v1.1.0` (già esistente)

**Release title:** 
```
ProxyAI v1.1.0 - UI Temi Light/Dark e Robustezza Migliorata
```

**Describe this release:** 
Copia il contenuto completo del file `RELEASE_NOTES_v1.1.0.md` che è stato creato.

### 3. Build del Binario

Prima di pubblicare la release, devi creare il binario da allegare:

```powershell
# Dalla root del progetto
dotnet publish ProxyAI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# Crea lo zip
Compress-Archive -Path publish\ProxyAI.exe -DestinationPath publish\ProxyAI-v1.1.0-win-x64.zip
```

### 4. Allega il Binario

- Nella sezione "Attach binaries" della release
- Trascina il file `publish\ProxyAI-v1.1.0-win-x64.zip`
- Opzionale: allega anche `ProxyAI.exe` direttamente se vuoi offrire entrambe le opzioni

### 5. Impostazioni Finali

- ✅ **Set as the latest release** (già il default)
- ⚠️ **NON** spuntare "Set as a pre-release"
- ✅ Verifica che il target branch sia `master`

### 6. Pubblica

Clicca su **"Publish release"**

## 🏷️ Gestione Tag Precedente (v1.0.0)

Il tag `v1.0.0` esiste già e punta al commit corretto. Se vuoi creare/aggiornare anche quella release:

1. Vai a: `https://github.com/mgiane86/ProxyAI/releases/new`
2. Choose a tag: `v1.0.0`
3. Release title: `ProxyAI v1.0.0 - Rilascio Iniziale`
4. Description: copia la sezione v1.0.0 dal `CHANGELOG.md`
5. Spunta **"Set as a pre-release"** oppure lasciala come release secondaria
6. Pubblica

## 📋 Checklist Post-Release

Dopo aver pubblicato la release v1.1.0:

- [ ] Verifica che la release appaia in `https://github.com/mgiane86/ProxyAI/releases`
- [ ] Verifica che il binario sia scaricabile
- [ ] Testa il download e l'esecuzione del binario
- [ ] Aggiorna eventuali link nel README se necessario
- [ ] Considera di pinnare la release su GitHub (stella nella pagina release)
- [ ] Opzionale: crea un annuncio/post se hai un blog o social

## 🔄 Branch Workflow Futuro

Dopo questa release, il workflow consigliato è:

1. **master** = versione stabile, release ufficiali
2. **development-MultiAgent** = sviluppo attivo
3. Nuove feature → branch feature → merge in development-MultiAgent
4. Quando pronto per release → merge development-MultiAgent in master → tag

## 📝 Note

- Il file `RELEASE_NOTES_v1.1.0.md` contiene il testo completo da copiare nella descrizione
- Il file `CHANGELOG.md` è già aggiornato con la data corretta (2026-10-23)
- I file temporanei `.tmp` creati dalla scrittura atomica sono automaticamente gestiti e non vanno versionati

---

**Repository**: https://github.com/mgiane86/ProxyAI
**Branch principale**: master
**Tag corrente**: v1.1.0
