// CimBios.Core libraries features example app

using System.IO.Compression;
using System.Reflection;
using CimBios.Core.CimModel.DataModel.Document;
using CimBios.Core.CimModel.DataModel.Utils;
using CimBios.Core.CimModel.DatatypeLib;
using CimBios.Core.CimModel.DatatypeLib.Factory;
using CimBios.Core.CimModel.DatatypeLib.ModelObject;
using CimBios.Core.CimModel.DatatypeLib.OID;
using CimBios.Core.CimModel.DatatypeLib.TypeLib;
using CimBios.Core.CimModel.RdfSerializer;
using CimBios.Core.CimModel.Schema;
using CimBios.Core.CimModel.Schema.RdfSchema;
using CimBiosCoreExampleApp;
using CimBiosCoreExampleApp.TypeLib;
using Serilog;

// 💡 Use serilog logger to diagnostics
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .CreateLogger();

// ---------------------------------------------------------------------------

// 💡 Load full CIM17 RDF schema
var schema = new CimRdfSchemaXmlFactory().CreateSchema(logger);
schema.Load(new StreamReader("Schemas\\cim17.rdfs"));

// 💡 Create type lib instance
// Schema is used to check registering assembly types
var typeLib = new CimDatatypeLib(schema, logger);
// Load this assembly to typelib to register C# implementations of CIM classes
// see CimClassImpl.cs
typeLib.LoadAssembly(Assembly.GetExecutingAssembly());

// 💡 Create OID descriptor factory - CIM objects identifiers factory. Uuid for example.
var oidDescriptorFactory = new UuidDescriptorFactory();
// OID descriptor is a namespace and id type specifying wrapper which identifying any object in CIM model

// 💡 Create CIM FullModel document
var document = new CimDocument(schema, typeLib, oidDescriptorFactory)
    .LogModelChanges(logger); // To demonstrate model changes tracking

// 💡 Create object with metamodel entities
var geoMetaClass = schema.TryGetResource<ICimMetaClass>(new Uri("http://iec.ch/TC57/CIM100#GeographicalRegion"));
var geoRegion = document.CreateObject(new UuidDescriptor(), geoMetaClass!);

var locationMetaClass = schema.TryGetResource<ICimMetaClass>(new Uri("http://iec.ch/TC57/CIM100#Location"));
var location = document.CreateObject(new UuidDescriptor(), locationMetaClass!);

// 💡 Create object with c# type wrapper
var substation = document.CreateObject<Substation>(new UuidDescriptor());

// Create object w/ concrete identifier
var baseVoltage115 = document.CreateObject<BaseVoltage>(
    new UuidDescriptor(new Guid("3f3ac268-6c9f-44a6-98b8-c3e53ee42f1a")));
baseVoltage115.nominalVoltage = 115.0f;

var voltageLevel115 = document.CreateObject<VoltageLevel>(new UuidDescriptor());
voltageLevel115.name = "VL 110 kV";
voltageLevel115.BaseVoltage = baseVoltage115;

// In strong typed ModelObject instances all associations are strictly two-side
if (baseVoltage115.VoltageLevel.Contains(voltageLevel115))
{
    logger.Information("Wow! Automatic inverse link!");
}

// 💡 Change multiple association
substation.AddToVoltageLevels(voltageLevel115);

// 💡 Change properties via generic interfaces
var identifiedObjectNameProperty =
    schema.TryGetResource<ICimMetaProperty>(
        new Uri("http://iec.ch/TC57/CIM100#IdentifiedObject.name"));

substation.SetAttribute(identifiedObjectNameProperty!, "New substation"); // with Meta property
substation.SetAssoc1To1("Location", location); // With short name link
substation.AddAssoc1ToM("VoltageLevels", voltageLevel115);
(location as DynamicModelObjectBase)!.AsDynamic()!.name = "Location"; // As dynamic

// 💡 Remove object from model
document.RemoveObject(geoRegion);

// 💡 Compound attributes
var someAsset = document.CreateObject<Asset>(new UuidDescriptor());
someAsset.InitializeCompoundAttribute("inUseDate");
someAsset.inUseDate!.inUseDate = DateTime.Now;

// 💡 Meta enum literals support
var someTerminal = document.CreateObject<Terminal>(new UuidDescriptor());
someTerminal.phases = PhaseCode.A;
// Or
var phaseCodeBLiteral = schema.TryGetResource<ICimMetaIndividual>(
    new Uri("http://iec.ch/TC57/CIM100#PhaseCode.B"));
var enumValue = typeLib.CreateEnumValueInstance(phaseCodeBLiteral!);
someTerminal.SetAttributeAsEnum("phases", enumValue!);

// Add full model header
var fullModel = document.CreateObject<FullModel>(new UuidDescriptor());
fullModel.created = DateTime.Now;
fullModel.description = "Example model";

// 💡 Export model to CIMXML format
document.Save("example.xml", new RdfXmlSerializerFactory());
// Check out result file in output project directory

// ---------------------------------------------------------------------------

// ! You can use any seek-support stream
var compressedFileStream = File.OpenRead("Models\\ieee8500.xml.gz");
var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
var reader = new StreamReader(decompressor);
var text = reader.ReadToEnd();

// 💡 Load CIMXML model
var ieee8500Document = new CimDocument(schema, typeLib, oidDescriptorFactory, logger);
ieee8500Document.Parse(text, new RdfXmlSerializerFactory());

logger.Information("{count} objects loaded", ieee8500Document.GetAllObjects().Count());

// 💡 Subscribe DifferenceModel
var coreLib = new CoreDatatypeLibFactory().Create(); // core typelib, which contains all necessary classes
var diffModel = new CimDifferenceModel(coreLib.Schema, coreLib, oidDescriptorFactory, logger);
coreLib.Schema.Namespaces.Add("cim", new Uri("http://iec.ch/TC57/CIM100#"));
diffModel.SubscribeToDataModelChanges(ieee8500Document);

// 💡 Access objects in model
var concreteObject = ieee8500Document.GetObject("d1937de0-7eb5-4629-a0de-358571b83b09");
logger.Information("d1937de0-7eb5-4629-a0de-358571b83b09 is {class}", concreteObject!.MetaClass);

var allTerminals = ieee8500Document.GetObjects<Terminal>().ToList();

// 💡 Import other model objects
ieee8500Document.ImportModelObjects(document);
var substationCopy = ieee8500Document.GetObject<Substation>(substation.OID);

// ---------------------------------------------------------------------------

// Save diffs made in ieee8500Document
diffModel.Save("diff.xml", new RdfXmlSerializerFactory());

return;

