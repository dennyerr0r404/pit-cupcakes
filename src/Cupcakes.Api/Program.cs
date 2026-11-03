using Cupcakes.Api;

// Ponto de entrada: monta a aplicação (ver App.cs) e inicia o servidor web.
var app = await App.CriarAsync(args);
app.Run();

/// <summary>Permite referenciar a aplicação nos testes.</summary>
public partial class Program;
