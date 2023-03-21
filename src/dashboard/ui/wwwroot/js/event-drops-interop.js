const baseConfig = {
    d3,
    line: {
        color: (_, i) => d3.interpolateCool((((i % 10 * 10) + Math.floor(i ? Math.log10(i) : 0) + 50) % 101) / 100)
    },
    zoom: {
        minimumScale: 1,
        restrictPan: true,
    },
    drop: {
        date: d => new Date(d.time)
    }
};

export function renderTimeline(el, dotnetRef, dataset, start, end) {
    const chart = eventDrops({
        ...baseConfig,
        range: {
            start: new Date(start),
            end: new Date(end)
        },
        drop: {
            ...baseConfig.drop,
            onMouseOver: async (e, cloudEvent) => await dotnetRef.invokeMethodAsync("ShowCloudEventTooltipOnMouseOver", cloudEvent, e.pageX, e.pageY, (e.pageX + 500 <= window.innerWidth || window.innerWidth <= 500) ? "right" : "left"),
            onMouseOut: async (_) => await dotnetRef.invokeMethodAsync("HideCloudEventTooltipOnMouseOut"),
            onClick: async (_, cloudEvent) => await dotnetRef.invokeMethodAsync("SelectCloudEventOnClick", cloudEvent)
        },
        label: {
            onMouseOver: async (e, lane) => await dotnetRef.invokeMethodAsync("ShowLabelTooltipOnMouseOver", `${lane.name} (${lane.data.length})`, e.pageX, e.pageY),
            onMouseOut: async (_) => await dotnetRef.invokeMethodAsync("HideLabelTooltipOnMouseOut"),
        }
    });
    d3
        .select(el)
        .data([dataset])
        .call(chart);
}