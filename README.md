# Ikoboost

Application Windows de monitoring et d'optimisation système. .NET 8 / WPF / MVVM.

## Fonctionnalités

| Écran | Données affichées |
|---|---|
| Dashboard | CPU, RAM, réseau, ping, température, disques, uptime |
| Matériel | Capteurs de température (ACPI + OHM WMI) |
| Réseau | Adaptateur, IP, ping multi-serveurs, débit, réparation |
| Processus | Liste, kill, priorité |
| Applications | Winget : liste, install, maj, désinstall |
| Optimisation | Nettoyage, DNS, profil d'alimentation, maintenance 1-clic |
| Paramètres | Thème, fréquence, alertes (JSON atomique) |

## Températures sans dépendance externe

La lecture des températures utilise uniquement `System.Management` (natif .NET) :

1. **OHM WMI** — si OpenHardwareMonitor tourne en arrière-plan, ses capteurs complets sont disponibles (CPU cores, GPU, disques).
2. **ACPI Thermal Zones** — fallback natif Windows, toujours présent, moins précis.
3. **Win32_TemperatureProbe** — fallback secondaire pour certains OEM.

> Pour des températures GPU/CPU core précises sans OHM : installer [OpenHardwareMonitor](https://openhardwaremonitor.org/) et le laisser tourner. Ikoboost détecte automatiquement son fournisseur WMI.

## Build

```bash
dotnet build IkoboostWpf.sln
```

## Publish self-contained x64

```bash
dotnet publish IkoboostWpf/IkoboostWpf.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ./publish
```

## Prérequis

- Windows 10/11 x64
- Droits administrateur (UAC manifeste inclus) — requis pour : kill process système, DNS, profils d'alimentation, réparation réseau

## Limites connues

- Températures GPU/CPU core indisponibles sans OHM ou capteur ACPI exposé.
- winget requis pour l'écran Applications (Microsoft Store → App Installer).
- Certains antivirus bloquent la lecture des modules de processus système.
