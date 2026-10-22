# Manual VM acceptance checklist

The automated tests prove that the XML is what we expect; only a real installation proves that
Windows Setup accepts it. Run this for every shipped preset in a Hyper-V VM before relying on it,
and after every upstream bump that changes the preset's baseline.

## Setup

1. Generate `autounattend.xml` from the preset (UI, or later the API/CLI).
2. Put it at the root of a small ISO or VHD, or of a USB stick image, attached alongside the Windows ISO.
3. Create a Generation 2 VM with Secure Boot and a virtual TPM, one empty disk (127 GB default is
   fine) and a network adapter. Boot from the Windows ISO.

## Checks for every preset

- [ ] Setup runs from boot to the desktop without asking anything, except where the preset says
      "ask during setup".
- [ ] The installed edition is the one the preset asks for (`Get-ComputerInfo -Property WindowsProductName`).
- [ ] The computer name is applied (`hostname`), or is random if the preset says so.
- [ ] Every account in the preset exists with the right group membership and can log on with its password.
- [ ] Automatic logon happens once if the preset asks for it, and not again after a restart.
- [ ] Display language, locale, keyboard layouts and home location match the preset.
- [ ] Each selected bloatware item is gone.
- [ ] Each custom script ran (check its effect, and the logs in `C:\Windows\Setup\Scripts\`).
- [ ] Unless `keepSensitiveFiles` is set, `C:\Windows\Panther\unattend.xml` is gone after first logon.

## Preset-specific checks

### win11-pro

- [ ] Setup does not complain about TPM, Secure Boot or RAM (requirement bypass) — also try once on a VM without a TPM.
- [ ] Setup stops at the partitioning page (this preset partitions by hand), and continues unattended afterwards.
- [ ] OOBE completes without a network connection (`bypassNetworkCheck`).

### win11-pro-scripted

- [ ] Setup does not show the Windows Setup wizard; `pe.cmd` wipes disk 0 and applies the Pro image.
- [ ] Disk 0 has EFI, MSR, Windows and recovery partitions (`Get-Partition`), and `reagentc /info` shows WinRE enabled.
- [ ] Both keyboard layouts (US, German) are available.
- [ ] `labuser` is a standard user, `labadmin` an administrator.

## PLAN.md "Verify" items this checklist resolves

- §6.3 Accounts on Server: not yet (Server presets arrive in phase 2).
- §6.2 Edition prompt when an image is selected without a key: not yet (phase 2).
- Phase 1 client presets: that the generated files install unattended on real media, which no
  automated test can show.
