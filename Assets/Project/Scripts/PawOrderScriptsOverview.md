# Paw&Order Scripts Overview

## Purpose
Este arquivo orienta a leitura geral dos scripts do projeto Paw&Order. O projeto está organizado em um módulo de investigação específico do jogo e em pacotes de ferramentas reutilizáveis.

## Main folders
- `Investigations`: contém os dados, runtime e ferramentas de Editor do sistema investigativo.
- `Tools`: contém sistemas genéricos reutilizáveis, como animação de UI, pooling, utilitários de Transform, TMP e ferramentas de criação.
- `BloomPulse.cs` e `CameraHandheld.cs`: scripts soltos de comportamento visual/câmera que ainda não pertencem a um módulo documentado.

## Architectural rule
O módulo `Investigations` pertence ao namespace `Fabula.PawOrder`. As ferramentas genéricas usam namespaces próprios, como `Iung.Animation` e `Iung.Tools.Shared`.

## Prefer
- Manter sistemas específicos de Paw&Order dentro de `Investigations`.
- Manter ferramentas reaproveitáveis dentro de `Tools`.
- Criar documentação local no menor escopo útil quando uma pasta ganhar regras próprias.
- Preservar namespaces existentes ao modificar classes.

## Avoid
- Misturar lógica específica de investigação dentro de `Tools`.
- Criar scripts globais sem namespace quando eles fizerem parte de uma feature.
- Duplicar utilitários já existentes em `Tools/Shared`.
- Alterar nomes de assets ou contratos públicos sem revisar os fluxos de Editor que dependem deles.

## Notes for AI assistants
Antes de modificar código, leia primeiro os guias locais da pasta afetada. Se a alteração tocar `Investigations`, leia também `Investigations/InvestigationSystemGuide.md`. Se tocar `Tools`, leia o guia do pacote específico e `Tools/ToolsPackageGuide.md`.
