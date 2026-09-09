# WinProvision 0.6.0

1. Extraia todo o ZIP para uma pasta.
2. Instale o .NET 8 Desktop Runtime, caso ele ainda não esteja instalado.
3. Abra **WinProvision.exe**. Mantenha as pastas Assets e Scripts junto do executável.
4. Escolha **Português (Brasil)** ou **English** no campo **Idioma / Language**.

O idioma da interface é separado do idioma do Windows que será instalado. Você pode alternar os idiomas sem perder as configurações selecionadas. O idioma escolhido também é salvo nos perfis e levado ao seletor de aplicativos no primeiro acesso ao Windows.

## Criar o pendrive

Selecione uma ISO oficial, a versão do Windows e o dispositivo USB. Escolha um perfil Padrão, Recomendado ou Personalizado. Em Aplicativos, decida se quer selecionar os programas agora ou após a primeira inicialização.

Revise as escolhas, confirme que fez uma cópia dos arquivos do pendrive e digite a frase exibida. A criação apaga todas as partições do dispositivo USB selecionado. O disco de instalação do Windows será escolhido separadamente no PC de destino.

O perfil Recomendado remove Microsoft Solitaire Collection, Microsoft Notícias, Clima, Obter Ajuda, Hub de Comentários e Microsoft To Do, se estiverem presentes na imagem. Outras remoções ficam disponíveis em Personalizada.

## Testes e compatibilidade

Esta versão passou por testes de geração de XML, troca de idioma, preservação de escolhas, interface e simulações de gravação USB e instalação de aplicativos. Ainda é necessário validar a inicialização e a instalação completa em Windows 10 e 11.

O Builder exige Windows e .NET 8 Desktop Runtime. O seletor de aplicativos usa o Windows PowerShell e o WPF do próprio Windows. Os downloads de aplicativos exigem internet; alguns instaladores podem solicitar permissão ou interação.

Projeto e atualizações: https://github.com/SkysZin1/WinProvision-

