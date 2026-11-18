using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;
using Microsoft.AspNetCore.Components.Web;

namespace SolarUY.Integration.Tests;

// Este adaptador de pruebas se mantiene junto a la versión de Blazor del proyecto.
#pragma warning disable BL0006
// El renderer activa el ciclo interactivo de Blazor y permite usar los callbacks
// públicos de los controles sin reflejar campos o métodos privados de las pantallas.
internal sealed class RenderizadorInteractivo(IServiceProvider servicios, ILoggerFactory logs)
    : Renderer(servicios, logs)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    private int _raiz = -1;

    public async Task<T> AbrirAsync<T>(Dictionary<string, object?>? parametros = null) where T : IComponent
        => await Dispatcher.InvokeAsync(async () =>
        {
            if (_raiz >= 0) RemoveRootComponent(_raiz);
            var componente = (T)InstantiateComponent(typeof(T));
            _raiz = AssignRootComponentId(componente);
            await RenderRootComponentAsync(_raiz, parametros is null ? ParameterView.Empty : ParameterView.FromDictionary(parametros));
            return componente;
        });

    public T Control<T>(Func<T, bool> coincide) where T : IComponent
        => Assert.Single(Descendientes(_raiz).OfType<T>().Where(coincide));

    public Task PulsarAsync(string texto) => Dispatcher.InvokeAsync(() =>
        Control<RadzenButton>(b => b.Text == texto && !b.Disabled).Click.InvokeAsync());

    public Task EscribirAsync(string nombre, string valor) => Dispatcher.InvokeAsync(() =>
        Control<RadzenTextBox>(c => c.Name == nombre && !c.Disabled).ValueChanged.InvokeAsync(valor));

    public Task SeleccionarAsync<T>(string nombre, T valor) => Dispatcher.InvokeAsync(async () =>
    {
        var selector = Control<RadzenDropDown<T>>(c => c.Name == nombre && !c.Disabled);
        await selector.ValueChanged.InvokeAsync(valor);
        await selector.Change.InvokeAsync(valor);
    });

    public Task<string> TextoAsync() => Dispatcher.InvokeAsync(() => string.Join(" ", Textos(_raiz)));

    public Task EnviarAsync<T>() where T : class => Dispatcher.InvokeAsync(() =>
    {
        var formulario = Control<RadzenTemplateForm<T>>(_ => true);
        return formulario.Submit.InvokeAsync(formulario.Data);
    });

    private IEnumerable<IComponent> Descendientes(int id)
    {
        var frames = GetCurrentRenderTreeFrames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if (frame.FrameType != RenderTreeFrameType.Component) continue;
            yield return frame.Component;
            foreach (var hijo in Descendientes(frame.ComponentId))
                yield return hijo;
        }
    }

    private IEnumerable<string> Textos(int id)
    {
        var frames = GetCurrentRenderTreeFrames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Text: yield return frame.TextContent; break;
                case RenderTreeFrameType.Markup: yield return frame.MarkupContent; break;
                case RenderTreeFrameType.Component:
                    foreach (var texto in Textos(frame.ComponentId)) yield return texto;
                    break;
            }
        }
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

    protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
        IComponentActivator componentActivator, IComponentRenderMode renderMode)
        => renderMode is InteractiveServerRenderMode
            ? componentActivator.CreateInstance(componentType)
            : base.ResolveComponentForRenderMode(componentType, parentComponentId, componentActivator, renderMode);

    protected override void HandleException(Exception exception)
        => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception);
}
#pragma warning restore BL0006
