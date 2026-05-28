namespace PacCollector.Infrastructure.Plugins.Builtin;

public static class BuiltinSpecs
{
    public static readonly IReadOnlyList<PacFieldSpec> CommonFields = new PacFieldSpec[]
    {
        new("AnalyzerType", "Modelo del equipo", "", "Identificación"),
        new("AnalyzerSerialNumber", "N° de serie", "", "Identificación"),
        new("AnalyzerNumber", "ID del instrumento", "", "Identificación"),
        new("FirmwareVersion", "Versión de firmware", "", "Identificación"),
        new("SampleIdentifier", "ID de la muestra", "", "Identificación"),
        new("SampleType", "Tipo de muestra", "", "Identificación"),
        new("OperatorId", "Operador", "", "Identificación"),
        new("ProgramName", "Programa/método", "", "Identificación"),
        new("AnalyzerNetworkAddress", "IP del analizador", "", "Identificación"),
        new("StartRunDate", "Fecha de inicio", "", "Tiempos"),
        new("StartRunTime", "Hora de inicio", "", "Tiempos"),
        new("EndRunDate", "Fecha de fin", "", "Tiempos"),
        new("EndRunTime", "Hora de fin", "", "Tiempos"),
        new("DuringRunAlarm", "Códigos de alarma (bitmask)", "", "Tiempos"),
        new("EndOfTest", "Test terminó OK (1=sí)", "", "Tiempos"),
        new("AmbiantTemperatureAtStart", "Temperatura ambiente al iniciar", "°C", "Condiciones"),
        new("AtmosphericPressure", "Presión atmosférica al iniciar", "kPa", "Condiciones"),
        new("PressureUnit", "Unidad de presión", "", "Condiciones"),
        new("TemperatureUnit", "Unidad de temperatura", "", "Condiciones"),
        new("BarometricCorrection", "Corrección barométrica aplicada", "", "Condiciones"),
    };

    public static readonly PacInstrumentSpec OptiPmd = new(
        PluginId: "optipmd-builtin",
        DisplayName: "PAC OptiPMD",
        AnalyzerType: "OptiPMD",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("IBP", "Initial Boiling Point", "°C", "Destilación"),
            new("FBP", "Final Boiling Point", "°C", "Destilación"),
            new("FBPvolume", "% volumen al alcanzar FBP", "%", "Destilación"),
            new("Residue", "% residuo", "%", "Destilación"),
            new("Recovery", "% volumen recuperado total", "%", "Destilación"),
            new("ThermoCorrelation", "Correlación termodinámica", "", "Destilación"),
            new("HeatingAdjustment", "Ajuste de calentamiento", "", "Destilación"),
            new("CorrelationProduct", "Producto correlacionado", "", "Configuración"),
            new("StartTemperatureLimit", "Límite mínimo de T° inicial", "°C", "Configuración"),
            new("VolumeSpecification", "Spec de volumen", "", "Configuración"),
            new("TemperatureSpecification", "Spec de temperatura", "", "Configuración"),
            new("Ethanol", "Contenido de etanol", "%", "Configuración"),
        });

    public static readonly PacInstrumentSpec OptiCpp = new(
        PluginId: "opticpp-builtin",
        DisplayName: "PAC OptiCPP",
        AnalyzerType: "OptiCPP",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("CloudpointResult", "Cloud Point", "°C", "Resultado"),
            new("CloudpointSpec", "Spec del Cloud Point", "°C", "Configuración"),
            new("Cloudpoint_EndOfTest", "Test terminó OK", "", "Resultado"),
            new("Cloud_Result", "Resultado nube", "°C", "Resultado"),
            new("Cloud_ResultTime", "Tiempo de resultado", "s", "Resultado"),
            new("Cloud_StepNum", "N° de paso", "", "Resultado"),
            new("ExpectedCloud_point", "Cloud Point esperado", "°C", "Configuración"),
            new("NotroundedCloud_point", "Cloud Point sin redondear", "°C", "Resultado"),
        });

    public static readonly PacInstrumentSpec OptiFpp = new(
        PluginId: "optifpp-builtin",
        DisplayName: "PAC OptiFPP",
        AnalyzerType: "OptiFPP",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("Cfpp_Result", "CFPP", "°C", "Resultado"),
            new("Cfpp_ResultTime", "Tiempo de resultado", "s", "Resultado"),
            new("Cfpp_StepNum", "N° de paso", "", "Resultado"),
            new("Cfpp_ExpPP", "CFPP esperado", "°C", "Configuración"),
            new("Cfpp_Suctions", "Succiones", "", "Resultado"),
        });

    public static readonly PacInstrumentSpec OptiFzp = new(
        PluginId: "optifzp-builtin",
        DisplayName: "PAC OptiFZP",
        AnalyzerType: "OptiFZP",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("Freeze", "Freezing Point", "°C", "Resultado"),
        });

    public static readonly PacInstrumentSpec OptiMpp = new(
        PluginId: "optimpp-builtin",
        DisplayName: "PAC OptiMPP",
        AnalyzerType: "OptiMPP",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("PourpointResult", "Pour Point", "°C", "Resultado"),
            new("PourpointSpec", "Spec del Pour Point", "°C", "Configuración"),
            new("PourpointEndOfTest", "Test terminó OK", "", "Resultado"),
            new("Pour_Result", "Resultado pour point", "°C", "Resultado"),
            new("Pour_ResultTime", "Tiempo de resultado", "s", "Resultado"),
            new("Pour_StepNum", "N° de paso", "", "Resultado"),
            new("Pour_Tilts", "N° de inclinaciones", "", "Resultado"),
            new("ExpectedPourpoint", "Pour Point esperado", "°C", "Configuración"),
            new("CorrectedPourpoint", "Pour Point corregido", "°C", "Resultado"),
        });

    public static readonly PacInstrumentSpec OptiMvd = new(
        PluginId: "optimvd-builtin",
        DisplayName: "PAC OptiMVD",
        AnalyzerType: "OptiMVD",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("DynamicViscosity", "Viscosidad dinámica", "cP", "Resultado"),
            new("Density", "Densidad", "g/cm³", "Resultado"),
            new("Viscosity", "Viscosidad", "cSt", "Resultado"),
        });

    public static readonly PacInstrumentSpec OptiFuel = new(
        PluginId: "optifuel-builtin",
        DisplayName: "PAC OptiFuel",
        AnalyzerType: "OptiFuel",
        Vendor: "PAC Collector",
        Version: "0.1.0",
        FieldSpecs: new PacFieldSpec[]
        {
            new("CetaneNumber", "Cetane Number", "", "Resultado"),
            new("CetaneIndex", "Cetane Index", "", "Resultado"),
            new("Density", "Densidad", "g/cm³", "Resultado"),
            new("Benzene", "Benceno", "%", "Resultado"),
            new("Saturates", "Saturados", "%", "Resultado"),
            new("Olefins", "Olefinas", "%", "Resultado"),
            new("MonoAromatics", "Mono-aromáticos", "%", "Resultado"),
            new("DiAromatics", "Di-aromáticos", "%", "Resultado"),
            new("PolycyclicAromatics", "Aromáticos policíclicos", "%", "Resultado"),
            new("TriPlusAromatics", "Tri+ aromáticos", "%", "Resultado"),
            new("TotalAromatics", "Aromáticos totales", "%", "Resultado"),
            new("VOCPerform", "VOC Performance", "", "Resultado"),
            new("VOC", "VOC", "", "Resultado"),
        });

    public static readonly IReadOnlyList<PacInstrumentSpec> All = new[]
    {
        OptiPmd, OptiCpp, OptiFpp, OptiFzp, OptiMpp, OptiMvd, OptiFuel,
    };
}
