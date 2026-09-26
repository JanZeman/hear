using System;
using System.Collections.Generic;
using System.Linq;
using HearApp.Core.HearingEngine;
using HearApp.Core.Results;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>
    /// Builds the Results screen: one reusable scrollable page rendered in two contexts -
    /// "Overall" (reached from the bottom nav's Results tab) and "Post-session" (reached right
    /// after finishing any world) - per sources/Results/hear-results-handoff-v1.0. A world's own
    /// identity only ever changes the background atmosphere, never the page structure, so there is
    /// no separate per-world context: any current/future world works through the same generic
    /// <see cref="WorldArt"/> art pipeline Home already uses, rather than hand-special-casing just
    /// Tide Troubles and River Journey and leaving Paper Garden (or any later world) unhandled.
    ///
    /// A standalone class rather than more methods on <see cref="ShellUIController"/> (already
    /// 1600+ lines) - takes only the two things it needs (the screen container and the flow).
    /// </summary>
    public static class ResultsScreenBuilder
    {
        private const int BaselineReliableSessionsTarget = 10;
        private const int RecentSessionsShown = 3;
        private const int TrendWindow = 10;

        private static readonly Color CardColor = new(1f, 1f, 1f, 0.88f);
        private static readonly Color GoodGreen = new(0.20f, 0.62f, 0.42f, 1f);
        private static readonly Color AttentionAmber = new(0.80f, 0.55f, 0.15f, 1f);

        public static void Build(VisualElement screen, GameFlowController flow)
        {
            screen.Clear();
            screen.style.position = Position.Relative;
            screen.style.flexGrow = 1;

            bool postSession = flow.HasJustCompletedSession;
            string worldId = postSession ? WorldRegistry.Worlds[flow.SelectedWorldIndex].Id : null;
            var allHistory = SessionHistoryStore.LoadAll();

            if (allHistory.Count == 0 && !postSession)
            {
                BuildEmptyState(screen, flow);
                return;
            }

            screen.Add(BuildBackground(worldId));

            // The glass nav floats over this screen's own background now, which means it can only
            // show that background through its translucency - not scrolled card content sliding
            // past underneath it. Two fix attempts so far (content-side bottom padding; then
            // flexGrow + marginBottom on the ScrollView itself) both failed to stop content from
            // showing through at mid-scroll positions - only the final rested-at-the-end position
            // looked right. Now trying explicit absolute positioning (top/left/right/bottom) so
            // the ScrollView's own rendered box - and therefore its clip rect - is pinned above the
            // nav unambiguously, instead of relying on flex layout to propagate a shrink downward.
            bool compact = ResponsiveNavBar.BreakpointForWidth(screen.resolvedStyle.width) == ShellBreakpoint.Compact;
            // Exactly the nav's own top edge (its bottom offset plus its own height, matching
            // ShellUIController.ApplyBreakpointLayout's navBottom/_navHost math) - no added
            // breathing-room term, per human direction: the card should end flush against the
            // glass panel with no visible gap, not float above it.
            float navReserve = compact
                ? ShellUIController.GetBottomSafeAreaInsetLogical() + 4f + ResponsiveNavBar.CompactBarHeight
                : VisualTokens.Spacing.L + ShellUIController.GetBottomSafeAreaInsetLogical();

            // The box-size fix alone (absolute position + explicit bottom=navReserve, confirmed
            // correct via a layout dump: scroll.layout.height was exactly screen.height minus
            // navReserve) still let content paint past that box - ScrollView's own outer element
            // does not clip to its box by default, only its internal viewport does something with
            // scroll range, not visual clipping. Overflow.Hidden here is what actually stops
            // anything rendering outside this element's box, regardless of ScrollView's own box
            // being sized correctly.
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    position = Position.Absolute, left = 0, right = 0, top = 0, bottom = navReserve,
                    overflow = Overflow.Hidden
                }
            };
            scroll.contentContainer.style.paddingLeft = VisualTokens.Spacing.L;
            scroll.contentContainer.style.paddingRight = VisualTokens.Spacing.L;
            scroll.contentContainer.style.paddingTop = VisualTokens.Spacing.XXL;
            // No bottom padding here: the scroll box itself already ends exactly at the nav's top
            // edge (navReserve above), so any padding here would just reopen the gap that was
            // just closed.
            screen.Add(scroll);
            var content = scroll.contentContainer;

            content.Add(BuildHeader());
            content.Add(BuildNonMedicalReminder());

            var recentForTrend = SessionHistoryStore.Recent(TrendWindow);
            float overallAge = SessionHistoryStore.OverallHearingAge(recentForTrend);
            int reliableCount = SessionHistoryStore.ReliableCount(recentForTrend);
            var latest = allHistory[allHistory.Count - 1];

            if (postSession)
            {
                content.Add(BuildPostSessionSummaryCard(latest, overallAge));
                content.Add(BuildHearingProfilePreview(latest.FrequencyTrials));
                content.Add(BuildMeasurementQualityCard(HearingAgeEstimator.Describe(latest)));
                content.Add(BuildFullHearingProfileCard(latest.FrequencyTrials));
                content.Add(BuildProgressCard(recentForTrend));
                content.Add(BuildRecentSessionsCard(allHistory));
                content.Add(BuildTodaysSessionCard(latest));
                content.Add(BuildBackToWorldsUtility(flow));
            }
            else
            {
                content.Add(BuildOverallSummaryCard(overallAge, reliableCount));
                content.Add(BuildHearingProfilePreview(latest.FrequencyTrials));
                content.Add(BuildMeasurementQualityCard(HearingAgeEstimator.Describe(latest)));
                content.Add(BuildFullHearingProfileCard(latest.FrequencyTrials));
                content.Add(BuildProgressCard(recentForTrend));
                content.Add(BuildRecentSessionsCard(allHistory));
                content.Add(BuildAboutResultsCard());
                content.Add(BuildResetHistoryUtility(() => Build(screen, flow)));
            }
        }

        // ---------------------------------------------------------------- Empty state

        private static void BuildEmptyState(VisualElement screen, GameFlowController flow)
        {
            screen.Add(BuildBackground(null));

            // Same glass-nav reservation as the populated Build() path (see its own comment) -
            // the empty state has no ScrollView, but its centered card group could still land low
            // enough on a short screen to sit under the floating nav without this.
            bool compact = ResponsiveNavBar.BreakpointForWidth(screen.resolvedStyle.width) == ShellBreakpoint.Compact;
            // Exactly the nav's own top edge (its bottom offset plus its own height, matching
            // ShellUIController.ApplyBreakpointLayout's navBottom/_navHost math) - no added
            // breathing-room term, per human direction: the card should end flush against the
            // glass panel with no visible gap, not float above it.
            float navReserve = compact
                ? ShellUIController.GetBottomSafeAreaInsetLogical() + 4f + ResponsiveNavBar.CompactBarHeight
                : VisualTokens.Spacing.L + ShellUIController.GetBottomSafeAreaInsetLogical();

            var content = new VisualElement
            {
                style =
                {
                    flexGrow = 1, paddingLeft = VisualTokens.Spacing.L, paddingRight = VisualTokens.Spacing.L,
                    paddingTop = VisualTokens.Spacing.XXL, paddingBottom = navReserve + VisualTokens.Spacing.L
                }
            };
            content.Add(BuildHeader());
            content.Add(BuildNonMedicalReminder());

            // The card group is only ever a few hundred px tall on a phone-height screen, so it is
            // wrapped in its own flexGrow:1/Center container instead of just stacking top-down -
            // otherwise, per human on-device feedback 2026-09-26, it reads as broken/unfinished,
            // stranded at the top with a huge dead gap below it before the nav bar.
            var centerGroup = new VisualElement { style = { flexGrow = 1, justifyContent = Justify.Center } };

            var card = MakeCard();
            card.style.alignItems = Align.Center;
            card.Add(MakeLabel("No result yet", VisualTokens.Type.Headline, VisualTokens.Colors.Ink900, VisualTokens.Spacing.M));

            // Small decorative upward sparkline, purely illustrative (no real data exists yet) -
            // echoes the empty-state reference's own little growth-curve icon, reusing the same
            // LineChartElement the populated screen's real charts use rather than a static image.
            var sparkline = new LineChartElement
            {
                style = { height = 48, width = Length.Percent(60f), marginTop = VisualTokens.Spacing.S }
            };
            sparkline.SetSeries(new List<LineChartElement.Series>
            {
                new(new List<float> { 0.15f, 0.2f, 0.35f, 0.55f, 0.9f }, new Color(0.6f, 0.66f, 0.78f, 0.6f))
            });
            card.Add(sparkline);

            card.Add(MakeLabel("Play your first world to create your baseline.", VisualTokens.Type.Body,
                VisualTokens.Colors.Ink700, VisualTokens.Spacing.M));
            var footerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = VisualTokens.Spacing.M } };
            footerRow.Add(new VisualElement
            {
                style =
                {
                    width = 6, height = 6, backgroundColor = GoodGreen, marginRight = VisualTokens.Spacing.S,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            });
            var footerLabel = MakeLabel("Results will appear after your first reliable session.", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400);
            footerLabel.style.flexShrink = 1;
            footerRow.Add(footerLabel);
            card.Add(footerRow);
            centerGroup.Add(card);

            var playBtn = new Button(() => flow.ReturnToSelectorFromResults());
            StyleUtilityButton(playBtn, destructive: false);
            playBtn.style.flexDirection = FlexDirection.Row;
            playBtn.style.justifyContent = Justify.Center;
            playBtn.style.alignItems = Align.Center;
            playBtn.Add(new VisualElement
            {
                style =
                {
                    width = 22, height = 22, marginRight = VisualTokens.Spacing.S,
                    backgroundImage = WorldArt.Icon("play"), unityBackgroundImageTintColor = Color.white
                }
            });
            var playBtnLabel = new Label("Play your first world") { style = { color = Color.white, fontSize = VisualTokens.Type.BodyStrong.Size, unityFontStyleAndWeight = VisualTokens.Type.BodyStrong.Style } };
            ApplyLineHeight(playBtnLabel, VisualTokens.Type.BodyStrong.Size);
            playBtn.Add(playBtnLabel);
            centerGroup.Add(playBtn);

            var infoCard = MakeCard();
            infoCard.Add(MakeLabel("What you'll see here", VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink900));
            infoCard.Add(MakeLabel("Your hearing age, your latest session, and a simple hearing profile over time.",
                VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.XS));
            centerGroup.Add(infoCard);

            var infoCard2 = MakeCard();
            infoCard2.Add(MakeLabel("No sessions yet", VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink900));
            infoCard2.Add(MakeLabel("Complete your first world to see your history here.",
                VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.XS));
            centerGroup.Add(infoCard2);

            content.Add(centerGroup);
            screen.Add(content);
        }

        public static VisualElement BuildNonMedicalReminder()
        {
            var reminder = MakeCard();
            var label = MakeLabel("HEAR is not a medical device.", VisualTokens.Type.Body,
                VisualTokens.Colors.Ink700);
            label.style.whiteSpace = WhiteSpace.Normal;
            reminder.Add(label);
            return reminder;
        }

        // ---------------------------------------------------------------- Summary cards

        private static VisualElement BuildOverallSummaryCard(float overallAge, int reliableCount)
        {
            var card = MakeCard();
            card.Add(TitleWithInfoDot("Overall hearing age"));
            card.Add(BigNumberRow(Mathf.RoundToInt(overallAge)));

            bool stable = reliableCount >= BaselineReliableSessionsTarget;
            card.Add(ProgressBar(Mathf.Clamp01(reliableCount / (float)BaselineReliableSessionsTarget), stable ? GoodGreen : VisualTokens.Colors.AuroraBlue));

            if (stable)
            {
                card.Add(MakeLabel("Stable estimate", VisualTokens.Type.BodyStrong, GoodGreen, VisualTokens.Spacing.S));
                card.Add(MakeLabel($"Based on {reliableCount} reliable sessions", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
            }
            else
            {
                card.Add(MakeLabel("Building your baseline", VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink900, VisualTokens.Spacing.S));
                card.Add(MakeLabel($"{reliableCount} of {BaselineReliableSessionsTarget} reliable sessions", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
                card.Add(MakeLabel("More sessions will improve confidence", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
            }
            return card;
        }

        private static VisualElement BuildPostSessionSummaryCard(SessionHistoryEntry latest, float overallAge)
        {
            var card = MakeCard();
            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };

            var left = new VisualElement { style = { flexShrink = 1 } };
            left.Add(TitleWithInfoDot("This session"));
            left.Add(BigNumberRow(Mathf.RoundToInt(latest.HearingAgeEstimate)));
            topRow.Add(left);

            var right = new VisualElement { style = { alignItems = Align.FlexEnd } };
            right.Add(MakeLabel("Overall", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
            right.Add(MakeLabel($"{Mathf.RoundToInt(overallAge)}", VisualTokens.Type.Headline, VisualTokens.Colors.Ink900));
            topRow.Add(right);
            card.Add(topRow);

            // Self-referential comparison only (session vs. this player's own overall average) -
            // never an age-group comparison, per the handoff's explicit wording rule: the app has
            // no age input anywhere, so it cannot honestly know an "age group" to compare against.
            float delta = overallAge - latest.HearingAgeEstimate;
            string comparison;
            Color color = VisualTokens.Colors.Ink700;
            if (Mathf.Abs(delta) < 0.5f)
            {
                comparison = "About the same as your overall average.";
            }
            else if (delta > 0f)
            {
                comparison = $"About {Mathf.RoundToInt(delta)} years younger than your overall average.";
                color = GoodGreen;
            }
            else
            {
                comparison = $"About {Mathf.RoundToInt(-delta)} years higher than your overall average.";
            }
            card.Add(MakeLabel(comparison, VisualTokens.Type.Caption, color, VisualTokens.Spacing.S));
            return card;
        }

        private static VisualElement BigNumberRow(int years)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexEnd, marginTop = VisualTokens.Spacing.XS } };
            row.Add(MakeLabel(years.ToString(), new VisualTokens.TypeStyle(40, 700), VisualTokens.Colors.Ink900));
            row.Add(MakeLabel(" years", VisualTokens.Type.Body, VisualTokens.Colors.Ink700, 0, 2));
            return row;
        }

        private static VisualElement ProgressBar(float fraction, Color fillColor)
        {
            var track = new VisualElement
            {
                style =
                {
                    height = 6, backgroundColor = VisualTokens.Colors.Mist100, overflow = Overflow.Hidden,
                    marginTop = VisualTokens.Spacing.S,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            };
            var fill = new VisualElement
            {
                style =
                {
                    height = 6, width = Length.Percent(Mathf.Clamp01(fraction) * 100f), backgroundColor = fillColor,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            };
            track.Add(fill);
            return track;
        }

        // ---------------------------------------------------------------- Hearing profile

        // Preview and full sections are visually near-identical in the approved board - the same
        // tab-toggle, axis-labelled chart, just shorter above the fold - so both call this one
        // builder instead of the preview being a stripped-down, non-interactive glimpse.
        private static VisualElement BuildHearingProfilePreview(IReadOnlyList<FrequencyTrialRecord> trials) =>
            BuildHearingProfileSection("Hearing profile (preview)", trials, chartHeight: 90, withInfoDot: true);

        private static VisualElement BuildFullHearingProfileCard(IReadOnlyList<FrequencyTrialRecord> trials) =>
            BuildHearingProfileSection("Full hearing profile", trials, chartHeight: 140, withInfoDot: false);

        private static VisualElement BuildHearingProfileSection(string title, IReadOnlyList<FrequencyTrialRecord> trials, float chartHeight, bool withInfoDot)
        {
            var card = MakeCard();
            card.Add(ChevronHeader(title, withInfoDot));

            var tabRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = VisualTokens.Spacing.S, marginBottom = VisualTokens.Spacing.XS } };
            var chartHost = new VisualElement();
            var tabs = new List<(Button btn, EarChannel ch)>();
            EarChannel selected = EarChannel.Combined;

            void Rebuild()
            {
                chartHost.Clear();
                chartHost.Add(BuildFrequencyChart(trials, selected, height: chartHeight, showAxis: true));
            }

            void Restyle()
            {
                foreach (var (btn, ch) in tabs)
                {
                    bool active = ch == selected;
                    btn.style.backgroundColor = active ? VisualTokens.Colors.AuroraBlue : VisualTokens.Colors.Mist100;
                    btn.style.color = active ? Color.white : VisualTokens.Colors.Ink700;
                }
            }

            Button MakeTab(string label, EarChannel ch)
            {
                var b = new Button(() => { selected = ch; Rebuild(); Restyle(); }) { text = label };
                b.style.marginRight = VisualTokens.Spacing.S;
                b.style.paddingLeft = VisualTokens.Spacing.M; b.style.paddingRight = VisualTokens.Spacing.M;
                b.style.paddingTop = 4; b.style.paddingBottom = 4;
                b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0; b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0;
                b.style.borderTopLeftRadius = VisualTokens.Radius.Pill; b.style.borderTopRightRadius = VisualTokens.Radius.Pill;
                b.style.borderBottomLeftRadius = VisualTokens.Radius.Pill; b.style.borderBottomRightRadius = VisualTokens.Radius.Pill;
                b.style.fontSize = VisualTokens.Type.Caption.Size;
                tabs.Add((b, ch));
                return b;
            }

            tabRow.Add(MakeTab("Left ear", EarChannel.Left));
            tabRow.Add(MakeTab("Right ear", EarChannel.Right));
            tabRow.Add(MakeTab("Both", EarChannel.Combined));
            card.Add(tabRow);
            card.Add(chartHost);
            Rebuild();
            Restyle();
            return card;
        }

        /// <summary>Plots detection rate (not a clinical dB threshold - this engine presents tones
        /// at one fixed level, so a real threshold audiogram isn't something we can honestly draw
        /// yet) per tested frequency, for the given ear filter. The shaded band marks a "reliably
        /// detected" zone (70-100%) rather than reusing the mockup's "Louder/Quieter sounds"
        /// loudness-threshold labels, which would overstate what a single-level detection rate
        /// actually measures.</summary>
        private static VisualElement BuildFrequencyChart(IReadOnlyList<FrequencyTrialRecord> trials, EarChannel channel, float height, bool showAxis)
        {
            var host = new VisualElement { style = { marginTop = VisualTokens.Spacing.XS } };
            var freqs = (trials ?? Array.Empty<FrequencyTrialRecord>())
                .Select(t => t.FrequencyHz).Distinct().OrderBy(f => f).ToList();

            if (freqs.Count == 0)
            {
                host.Add(MakeLabel("Not enough data yet.", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
                return host;
            }

            var values = new List<float>(freqs.Count);
            foreach (var f in freqs)
            {
                var atFreq = trials.Where(t => t.FrequencyHz == f && (channel == EarChannel.Combined || t.Channel == channel)).ToList();
                values.Add(atFreq.Count == 0 ? 0f : atFreq.Count(t => t.Detected) / (float)atFreq.Count);
            }

            var chart = new LineChartElement { style = { height = height } };
            chart.SetBand(0.7f, 1f);
            chart.SetSeries(new List<LineChartElement.Series> { new(values, VisualTokens.Colors.AuroraBlue) });
            host.Add(chart);

            if (showAxis)
            {
                var axisRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginTop = VisualTokens.Spacing.XS } };
                foreach (var f in freqs)
                    axisRow.Add(MakeLabel(FormatFrequency(f), VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
                host.Add(axisRow);
            }
            return host;
        }

        private static string FormatFrequency(float hz) => hz >= 1000f ? $"{hz / 1000f:0.#}k" : $"{hz:0}";

        private static VisualElement BuildMeasurementQualityCard(HearingAgeEstimator.QualitySummary summary)
        {
            var card = MakeCard();
            card.Add(ChevronHeader("Measurement quality", withInfoDot: true));
            Color color = summary.Level switch
            {
                HearingAgeEstimator.QualityLevel.Reliable => GoodGreen,
                HearingAgeEstimator.QualityLevel.NeedsAttention => AttentionAmber,
                _ => VisualTokens.Colors.Slate400
            };
            card.Add(MakeLabel(summary.Headline, VisualTokens.Type.BodyStrong, color, VisualTokens.Spacing.S));
            card.Add(MakeLabel(summary.Detail, VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
            return card;
        }

        // ---------------------------------------------------------------- Progress / history

        private static VisualElement BuildProgressCard(IReadOnlyList<SessionHistoryEntry> recentNewestFirst)
        {
            var card = MakeCard();
            card.Add(ChevronHeader("Your progress"));
            card.Add(MakeLabel("Hearing age trend", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));

            if (recentNewestFirst.Count < 2)
            {
                card.Add(MakeLabel("Play a few more sessions to see a trend.", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.S));
                return card;
            }

            var chronological = recentNewestFirst.Reverse().ToList();
            float min = chronological.Min(e => e.HearingAgeEstimate);
            float max = chronological.Max(e => e.HearingAgeEstimate);
            float range = Mathf.Max(1f, max - min);
            // Inverted on purpose: a lower hearing-age estimate plots higher on the chart, so an
            // improving trend reads as an upward line, matching the approved board's own trend.
            var values = chronological.Select(e => 1f - (e.HearingAgeEstimate - min) / range).ToList();

            var chart = new LineChartElement { style = { height = 100, marginTop = VisualTokens.Spacing.S } };
            chart.SetSeries(new List<LineChartElement.Series> { new(values, VisualTokens.Colors.AuroraIris) });
            card.Add(chart);
            return card;
        }

        private static VisualElement BuildRecentSessionsCard(IReadOnlyList<SessionHistoryEntry> allHistory)
        {
            var card = MakeCard();
            card.Add(ChevronHeader("Recent sessions"));

            var recent = allHistory.Reverse().Take(RecentSessionsShown).ToList();
            if (recent.Count == 0)
            {
                card.Add(MakeLabel("No sessions yet.", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.S));
                return card;
            }

            foreach (var entry in recent)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = VisualTokens.Spacing.S } };

                var thumb = new VisualElement
                {
                    style =
                    {
                        width = 40, height = 40, overflow = Overflow.Hidden, unityBackgroundScaleMode = ScaleMode.ScaleAndCrop,
                        borderTopLeftRadius = 8, borderTopRightRadius = 8, borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                        backgroundColor = VisualTokens.Colors.Mist100
                    }
                };
                var thumbTex = WorldArt.GetWorldTextures(entry.WorldId).Card43;
                if (thumbTex != null) thumb.style.backgroundImage = new StyleBackground(thumbTex);
                row.Add(thumb);

                string displayName = WorldRegistry.Worlds.FirstOrDefault(w => w.Id == entry.WorldId).DisplayName ?? entry.WorldId;
                var textCol = new VisualElement { style = { marginLeft = VisualTokens.Spacing.S, flexGrow = 1 } };
                textCol.Add(MakeLabel(displayName, VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink900));
                textCol.Add(MakeLabel(entry.TimestampUtc.ToLocalTime().ToString("d MMM yyyy"), VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
                row.Add(textCol);

                row.Add(MakeLabel($"{Mathf.RoundToInt(entry.HearingAgeEstimate)} years", VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink700));
                card.Add(row);
            }
            return card;
        }

        private static VisualElement BuildTodaysSessionCard(SessionHistoryEntry latest)
        {
            var card = MakeCard();
            card.Add(ChevronHeader("Today's session"));

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginTop = VisualTokens.Spacing.S } };
            row.Add(StatColumn(latest.CorrectDetections.ToString(), "Detected", VisualTokens.Colors.AuroraBlue));
            row.Add(StatColumn($"{Mathf.RoundToInt(latest.DetectionRate * 100f)}%", "Reaction rate", GoodGreen));
            row.Add(StatColumn(latest.Misses.ToString(), "Missed", AttentionAmber));
            card.Add(row);
            return card;
        }

        // Small colored dot above each number, echoing the approved board's colored per-stat
        // icons (fish/combo/target-style glyphs this codebase has no matching art for).
        private static VisualElement StatColumn(string value, string label, Color accent)
        {
            var col = new VisualElement { style = { alignItems = Align.Center } };
            col.Add(new VisualElement
            {
                style =
                {
                    width = 8, height = 8, backgroundColor = accent, marginBottom = VisualTokens.Spacing.XS,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            });
            col.Add(MakeLabel(value, VisualTokens.Type.Headline, VisualTokens.Colors.Ink900));
            col.Add(MakeLabel(label, VisualTokens.Type.Caption, VisualTokens.Colors.Slate400));
            return col;
        }

        private static VisualElement BuildAboutResultsCard()
        {
            var card = MakeCard();
            var header = ChevronHeader("About your results");
            // Small amber "lightbulb" accent dot - approved board leads this row with a lightbulb
            // icon this codebase has no matching art for.
            header.Children().First().Insert(0, ColorDot(AttentionAmber, marginRight: true));
            card.Add(header);
            card.Add(MakeLabel(
                "Based on your tone-detection pattern across frequencies, not a clinical diagnosis. Estimates improve as you play more sessions.",
                VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.S));
            return card;
        }

        private static VisualElement BuildResetHistoryUtility(Action onReset)
        {
            var card = MakeCard();
            card.Add(MakeLabel("Clear all results and start fresh.", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, 0, VisualTokens.Spacing.S));
            var btn = new Button(() => { SessionHistoryStore.ClearAll(); onReset(); });
            StyleUtilityButton(btn, destructive: true);
            // Button.text creates its own internal label lazily, which fought Insert()'s ordering
            // (the dot kept rendering after the text no matter the insert index) - build the row
            // by hand instead, the same [icon, label] pattern the empty state's Play button
            // already uses successfully.
            btn.style.flexDirection = FlexDirection.Row; btn.style.justifyContent = Justify.Center; btn.style.alignItems = Align.Center;
            btn.Add(ColorDot(new Color(0.75f, 0.20f, 0.20f, 1f), marginRight: true));
            var resetLabel = new Label("Reset history") { style = { color = new Color(0.75f, 0.20f, 0.20f, 1f), fontSize = VisualTokens.Type.BodyStrong.Size, unityFontStyleAndWeight = VisualTokens.Type.BodyStrong.Style } };
            ApplyLineHeight(resetLabel, VisualTokens.Type.BodyStrong.Size);
            btn.Add(resetLabel);
            card.Add(btn);
            return card;
        }

        /// <summary>Small colored circle used as a lightweight stand-in for the approved board's
        /// per-row icons (lightbulb, trash, play) where this codebase has no matching icon art.</summary>
        private static VisualElement ColorDot(Color color, bool marginRight)
        {
            return new VisualElement
            {
                style =
                {
                    width = 8, height = 8, backgroundColor = color,
                    marginRight = marginRight ? VisualTokens.Spacing.S : 0f,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };
        }

        private static VisualElement BuildBackToWorldsUtility(GameFlowController flow)
        {
            var card = MakeCard();
            var btn = new Button(() => flow.ReturnToSelectorFromResults());
            StyleUtilityButton(btn, destructive: false);
            btn.style.flexDirection = FlexDirection.Row; btn.style.justifyContent = Justify.Center; btn.style.alignItems = Align.Center;
            btn.Add(ColorDot(Color.white, marginRight: true));
            var backLabel = new Label("Back to Worlds") { style = { color = Color.white, fontSize = VisualTokens.Type.BodyStrong.Size, unityFontStyleAndWeight = VisualTokens.Type.BodyStrong.Style } };
            ApplyLineHeight(backLabel, VisualTokens.Type.BodyStrong.Size);
            btn.Add(backLabel);
            card.Add(btn);
            return card;
        }

        // ---------------------------------------------------------------- Shared building blocks

        /// <summary>"Results" page title plus the small centered HEAR lockup + "Your hearing
        /// journey" tagline every context in the approved board carries at the top - previously
        /// just the bare title. Uses the on-dark full lockup (Home reserves the no-claim variant
        /// for itself; this screen isn't Home, so the with-claim-less-but-full mark reads fine at
        /// a small size on a dark/atmospheric background).</summary>
        private static VisualElement BuildHeader()
        {
            var container = new VisualElement { style = { marginBottom = VisualTokens.Spacing.L, flexShrink = 0 } };
            container.Add(MakeLabel("Results", VisualTokens.Type.Title, Color.white, 0, VisualTokens.Spacing.M));

            var brand = new VisualElement { style = { alignItems = Align.Center } };
            var logoTex = WorldArt.LogoOnDark;
            if (logoTex != null)
            {
                const float logoW = 110f;
                brand.Add(new VisualElement
                {
                    style =
                    {
                        width = logoW, height = logoW * logoTex.height / logoTex.width,
                        backgroundImage = new StyleBackground(logoTex), unityBackgroundScaleMode = ScaleMode.ScaleToFit
                    }
                });
            }
            brand.Add(MakeLabel("Your hearing journey", VisualTokens.Type.Caption, new Color(1f, 1f, 1f, 0.82f), VisualTokens.Spacing.XS));
            container.Add(brand);
            return container;
        }

        private static VisualElement BuildBackground(string worldId)
        {
            var host = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 },
                pickingMode = PickingMode.Ignore
            };

            if (worldId == null)
            {
                // No aurora photo asset has been supplied for the neutral Overall background - a
                // calm procedural gradient in the visual bible's own Aurora colors stays on-brand
                // without inventing artwork (see GradientBackgroundElement).
                host.Add(new GradientBackgroundElement(
                    new Color(0.08f, 0.11f, 0.20f, 1f),
                    new Color(0.16f, 0.20f, 0.33f, 1f),
                    new Color(0.28f, 0.24f, 0.40f, 1f))
                { style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 } });
                return host;
            }

            var textures = WorldArt.GetWorldTextures(worldId);
            var source = textures.Wide169 != null ? textures.Wide169 : textures.Ambient169;
            var treated = WorldArt.GetTreatedAmbient(source);
            var img = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0,
                    unityBackgroundScaleMode = ScaleMode.ScaleAndCrop
                },
                pickingMode = PickingMode.Ignore
            };
            if (treated != null) img.style.backgroundImage = ToStyleBackground(treated);
            host.Add(img);
            return host;
        }

        private static StyleBackground ToStyleBackground(Texture tex) => tex switch
        {
            RenderTexture rt => Background.FromRenderTexture(rt),
            Texture2D t2d => Background.FromTexture2D(t2d),
            _ => default
        };

        private static VisualElement MakeCard()
        {
            return new VisualElement
            {
                style =
                {
                    backgroundColor = CardColor,
                    borderTopLeftRadius = VisualTokens.Radius.L, borderTopRightRadius = VisualTokens.Radius.L,
                    borderBottomLeftRadius = VisualTokens.Radius.L, borderBottomRightRadius = VisualTokens.Radius.L,
                    paddingTop = VisualTokens.Spacing.L, paddingBottom = VisualTokens.Spacing.L,
                    paddingLeft = VisualTokens.Spacing.L, paddingRight = VisualTokens.Spacing.L,
                    marginBottom = VisualTokens.Spacing.M,
                    // Root cause of the "silene zmatecna" overlap (human report 2026-09-26,
                    // confirmed via a logged layout dump on a Galaxy S9+): UI Toolkit's default
                    // flex-shrink is 1, so once the stacked cards' natural total height exceeds
                    // the ScrollView's own viewport height, every card gets flex-shrunk to fit
                    // instead of the ScrollView actually scrolling - rows with no text content of
                    // their own (the big-number row, progress bars, tab rows) got crushed to
                    // literally 0px while plain Labels (which happened to already need
                    // flex-shrink:0 for a different, now-superseded fix) refused to shrink and
                    // painted over their now-collapsed siblings. Cards refusing to shrink is what
                    // actually matters - once a card keeps its natural height, nothing inside it
                    // is ever squeezed in the first place.
                    flexShrink = 0
                }
            };
        }

        private static VisualElement ChevronHeader(string title, bool withInfoDot = false)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            titleRow.Add(MakeLabel(title, VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink900));
            if (withInfoDot) titleRow.Add(InfoDot());
            row.Add(titleRow);
            row.Add(MakeLabel(">", VisualTokens.Type.Body, VisualTokens.Colors.Slate400));
            return row;
        }

        private static VisualElement TitleWithInfoDot(string title)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(MakeLabel(title, VisualTokens.Type.BodyStrong, VisualTokens.Colors.Ink700));
            row.Add(InfoDot());
            return row;
        }

        /// <summary>Small "i" info-circle glyph the approved board shows beside several card
        /// titles (Overall hearing age, This session, Hearing profile, Measurement quality) - a
        /// plain drawn circle+label rather than a font glyph (Unicode "ⓘ" risks the same
        /// unreliable text-shaping category of bug already found and fixed once in this file).</summary>
        private static VisualElement InfoDot()
        {
            var dot = new VisualElement
            {
                style =
                {
                    width = 15, height = 15, marginLeft = VisualTokens.Spacing.XS,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8, borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = VisualTokens.Colors.Slate400, borderBottomColor = VisualTokens.Colors.Slate400,
                    borderLeftColor = VisualTokens.Colors.Slate400, borderRightColor = VisualTokens.Colors.Slate400,
                    alignItems = Align.Center, justifyContent = Justify.Center
                }
            };
            var i = new Label("i") { style = { fontSize = 10, color = VisualTokens.Colors.Slate400, unityTextAlign = TextAnchor.MiddleCenter } };
            i.style.height = 12;
            dot.Add(i);
            return dot;
        }

        private static Label MakeLabel(string text, VisualTokens.TypeStyle style, Color color, float marginTop = 0f, float marginBottom = 0f)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = style.Size, unityFontStyleAndWeight = style.Style, color = color,
                    marginTop = marginTop, marginBottom = marginBottom, whiteSpace = WhiteSpace.Normal
                }
            };
            ApplyLineHeight(label, style.Size);
            return label;
        }

        // Plain auto-height Labels in a Column flex layout measured with the WRONG (far too
        // short, sometimes near-zero) intrinsic height on-device - confirmed on a Galaxy S9+
        // (human report 2026-09-26: "silene zmatecna", every card's rows painting on top of each
        // other instead of stacking) while this exact page's Buttons (which get real height from
        // their own padding, not text measurement) laid out fine. Root cause not chased further
        // (likely this runtime UI Toolkit build's text-shaping/measurement, per the "No ICU data
        // provided" warning logcat shows on every launch) - worked around by giving every label an
        // explicit height from its own font size instead of trusting auto-measurement, the same
        // "don't rely on intrinsic sizing" defensive pattern the rest of the shell already uses
        // (Home's cards are all explicitly positioned/sized, never auto-height).
        private static void ApplyLineHeight(VisualElement el, int fontSize)
        {
            // minHeight, not height: a floor for whatever the (apparently unreliable) auto
            // measurement returns, without capping genuinely-wrapped multi-line captions - those
            // already measured correctly even on the affected device (confirmed: the empty
            // state's two-line caption rendered fine there), so only the collapsing-to-near-zero
            // failure needs a rescue, not a hard cap.
            el.style.minHeight = fontSize * 1.45f;
            el.style.flexShrink = 0;
        }

        private static void StyleUtilityButton(Button b, bool destructive)
        {
            b.style.backgroundColor = destructive ? new Color(0.85f, 0.30f, 0.30f, 0.12f) : VisualTokens.Colors.AuroraBlue;
            b.style.color = destructive ? new Color(0.75f, 0.20f, 0.20f, 1f) : Color.white;
            b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0; b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0;
            b.style.borderTopLeftRadius = VisualTokens.Radius.S; b.style.borderTopRightRadius = VisualTokens.Radius.S;
            b.style.borderBottomLeftRadius = VisualTokens.Radius.S; b.style.borderBottomRightRadius = VisualTokens.Radius.S;
            b.style.paddingTop = VisualTokens.Spacing.M; b.style.paddingBottom = VisualTokens.Spacing.M;
            b.style.fontSize = VisualTokens.Type.BodyStrong.Size;
            b.style.unityFontStyleAndWeight = VisualTokens.Type.BodyStrong.Style;
            b.style.marginBottom = VisualTokens.Spacing.M;
        }
    }
}
