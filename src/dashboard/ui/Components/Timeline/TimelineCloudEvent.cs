namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Holds the data of a <see cref="CloudEvent"/> displayed in the <see cref="Timeline"/>
/// </summary>
public class TimelineCloudEvent
    : CloudEvent, ITimelineData
{
    /// <summary>
    /// Instanciates a new <see cref="TimelineCloudEvent"/>
    /// </summary>
    public TimelineCloudEvent() { }

    /// <summary>
    /// Instanciates a new <see cref="TimelineCloudEvent"/> based on a <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="cloudEvent">The <see cref="CloudEvent"/> to base the <see cref="TimelineCloudEvent"/> on</param>
    public TimelineCloudEvent(CloudEvent cloudEvent)
    {
        this.Id = cloudEvent.Id;
        this.SpecVersion = cloudEvent.SpecVersion;
        this.Time = cloudEvent.Time;
        this.Source = cloudEvent.Source;
        this.Type = cloudEvent.Type;
        this.Subject = cloudEvent.Subject;
        this.DataContentType = cloudEvent.DataContentType;
        this.DataSchema = cloudEvent.DataSchema;
        this.Data = cloudEvent.Data;
        this.DataBase64 = cloudEvent.DataBase64;
        this.ExtensionAttributes = cloudEvent.ExtensionAttributes;

    } 
    /// <summary>
    /// Gets/sets the date the <see cref="CloudEvent"/> occured
    /// </summary>
    public DateTimeOffset Date => this.Time.Value;
}

