using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace restapi.Models
{
    public class Timecard
    {
        public Timecard(int resource)
        {
            Resource = resource;
            UniqueIdentifier = Guid.NewGuid();
            Identity = new TimecardIdentity();
            Lines = new List<AnnotatedTimecardLine>();
            Transitions = new List<Transition> { 
                new Transition(new Entered() { Resource = resource }) 
            };
        }

        public int Resource { get; private set; }
        
        [JsonProperty("id")]
        public TimecardIdentity Identity { get; private set; }

        public TimecardStatus Status { 
            get 
            { 
                return Transitions
                    .OrderByDescending(t => t.OccurredAt)
                    .First()
                    .TransitionedTo;
            } 
        }

        public DateTime Opened;

        [JsonProperty("recId")]
        public int RecordIdentity { get; set; } = 0;

        [JsonProperty("recVersion")]
        public int RecordVersion { get; set; } = 0;

        public Guid UniqueIdentifier { get; set; }

        [JsonIgnore]
        public IList<AnnotatedTimecardLine> Lines { get; set; }

        [JsonIgnore]
        public IList<Transition> Transitions { get; set; }

        public IList<ActionLink> Actions { get => GetActionLinks(); }
    
        [JsonProperty("documentation")]
        public IList<DocumentLink> Documents { get => GetDocumentLinks(); }

        public string Version { get; set; } = "timecard-0.1";

        private IList<ActionLink> GetActionLinks()
        {
            var links = new List<ActionLink>();

            switch (Status)
            {
                case TimecardStatus.Draft:
                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.Cancellation,
                        Relationship = ActionRelationship.Cancel,
                        Reference = $"/timesheets/{Identity.Value}/cancellation"
                    });

                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.Submittal,
                        Relationship = ActionRelationship.Submit,
                        Reference = $"/timesheets/{Identity.Value}/submittal"
                    });

                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.TimesheetLine,
                        Relationship = ActionRelationship.RecordLine,
                        Reference = $"/timesheets/{Identity.Value}/lines"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Replace,
                        Relationship = ActionRelationship.Replace,
                        // Not sure of proper way to request {lineId} in href. 
                        // TODO: The functionality works, but revist later when time permits.
                        Reference = $"/timesheets/{Identity.Value}/replace/<lineId>"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Delete,
                        Type = ContentTypes.Deletion,
                        Relationship = ActionRelationship.Remove,
                        Reference = $"/timesheets/{Identity.Value}/delete"
                    });

                    break;

                case TimecardStatus.Submitted:
                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.Cancellation,
                        Relationship = ActionRelationship.Cancel,
                        Reference = $"/timesheets/{Identity.Value}/cancellation"
                    });

                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.Rejection,
                        Relationship = ActionRelationship.Reject,
                        Reference = $"/timesheets/{Identity.Value}/rejection"
                    });

                    links.Add(new ActionLink() {
                        Method = Method.Post,
                        Type = ContentTypes.Approval,
                        Relationship = ActionRelationship.Approve,
                        Reference = $"/timesheets/{Identity.Value}/approval"
                    });

                    break;

                case TimecardStatus.Approved:
                    // terminal state, nothing possible here
                    break;

                case TimecardStatus.Cancelled:
                    links.Add(new ActionLink()
                    {
                        Method = Method.Delete,
                        Type = ContentTypes.TimesheetLine,
                        Relationship = ActionRelationship.Remove,
                        Reference = $"/timesheets/{Identity.Value}/delete"
                    });

                    break;
            }

            return links;
        }

        private IList<DocumentLink> GetDocumentLinks()
        {
            var links = new List<DocumentLink>();

            links.Add(new DocumentLink() {
                Method = Method.Get,
                Type = ContentTypes.Transitions,
                Relationship = DocumentRelationship.Transitions,
                Reference = $"/timesheets/{Identity.Value}/transitions"
            });

            if (this.Lines.Count > 0)
            {
                links.Add(new DocumentLink() {
                    Method = Method.Get,
                    Type = ContentTypes.TimesheetLine,
                    Relationship = DocumentRelationship.Lines,
                    Reference = $"/timesheets/{Identity.Value}/lines"
                });
            }

            if (this.Status == TimecardStatus.Submitted)
            {
                links.Add(new DocumentLink() {
                    Method = Method.Get,
                    Type = ContentTypes.Transitions,
                    Relationship = DocumentRelationship.Submittal,
                    Reference = $"/timesheets/{Identity.Value}/submittal"
                });
            }

            return links;
        }

        /// <summary>
        /// Adds a new line to the timecard.
        /// </summary>
        /// <param name="timecardLine"></param>
        /// <param name="lineNum"></param>
        /// <returns></returns>
        public AnnotatedTimecardLine AddLine(TimecardLine timecardLine, int lineNum)
        {
            var annotatedLine = new AnnotatedTimecardLine(timecardLine);

            if (lineNum == 0)
            {
                // Set line number in timecard (0-based)
                annotatedLine.LineNumber = Lines.Count();
            } else
            {
                annotatedLine.LineNumber = lineNum;
            }

            Lines.Add(annotatedLine);

            return annotatedLine;
        }

        /// <summary>
        /// Replace an existing timecard line with a new one. Maintains the line number.
        /// </summary>
        /// <param name="timecardLine"></param>
        /// <param name="lineNum"></param>
        /// <returns></returns>
        public AnnotatedTimecardLine ReplaceLine(TimecardLine timecardLine, int lineNum)
        {
            AnnotatedTimecardLine oldLine = null;
            for (int i = 0; i < Lines.Count(); ++i)
            {
                if (Lines[i].LineNumber == lineNum)
                {
                    oldLine = Lines[i];
                    break;
                }
            }

            if (oldLine == null)
            {
                // Line number not found so nothing to replace
                return null;
            }

            // Stage new annotated line entry
            var newLine = new AnnotatedTimecardLine(timecardLine);

            // Maintain existing line number
            newLine.LineNumber = lineNum;

            // Perform the replacement
            Lines.Remove(oldLine);
            Lines.Add(newLine);

            return newLine;
        }
    }
}
