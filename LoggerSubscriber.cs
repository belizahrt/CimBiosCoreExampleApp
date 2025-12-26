using CimBios.Core.CimModel.DataModel.Document;
using Serilog;

namespace CimBiosCoreExampleApp;

public static class LoggerSubscriber
{
    public static CimDocument LogModelChanges(this CimDocument model, ILogger logger)
    {
        // Track storage change - add/remove objects in model
        model.ModelObjectStorageChanged += (sender, modelObject, args) =>
        {
            logger.Information("{operation} operation has been proceed " +
                               "on {oid} : {metaClass} in {model}",
                args.ChangeType, 
                modelObject.OID, 
                modelObject.MetaClass,
                sender?.GetHashCode());
        };
        
        // Track storage objects property changes
        model.ModelObjectPropertyChanged += (sender, modelObject, args) =>
        {
            logger.Information("{operation} operation on property {property} has been proceed " +
                               "on {oid} : {metaClass} in {model}",
                args.GetType().Name,
                args.MetaProperty,
                modelObject.OID, 
                modelObject.MetaClass,
                sender?.GetHashCode());
        };
        
        return model;
    }
}