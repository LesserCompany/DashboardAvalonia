using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Professionals;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LesserDashboardClient.Models.Company
{
    public class CreateNewClass : INotifyPropertyChanged
    {
        private string classCode;
        public string ClassCode { get => classCode;
            set
            {
                classCode = value;
                RaisePropertyChanged("ClassCode");
            }
        }

        private string eventsFolder;
        public string EventsFolder { get => eventsFolder;
            set
            {
                eventsFolder = value;
                RaisePropertyChanged("EventsFolder");
            }
        }

        private string recFolder;
        public string RecFolder { get => recFolder; set
            {
                recFolder = value;
                RaisePropertyChanged("RecFolder");
            }
        }

        public Professional Professional {
            get => professional; set
            {
                professional = value;
                RaisePropertyChanged("Professional");
            }
        }

        private Professional professional;

        public ProfessionalTask getProfessionalTask()
        {
            return new ProfessionalTask(professional.username, professional.company, ClassCode)
            {
                originalEventsFolder = EventsFolder,
                originalRecFolder = RecFolder
            };
        }
        

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }

    
}
