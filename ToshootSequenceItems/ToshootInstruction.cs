using Newtonsoft.Json;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Profile.Interfaces;
using NINA.Astrometry;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Equipment.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Equipment.Interfaces.Mediator;
using System.IO;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Container;
using System.Globalization;


namespace Cyrilastro.NINA.Toshoot.ToshootTestCategory {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Instruction to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceItem which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceItem is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "ToShoot")]
    [ExportMetadata("Description", "This item will just show a notification and is just there to show how the plugin system works")]
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "Toshoot")]
    [Export(typeof(ISequenceItem))]    
    [JsonObject(MemberSerialization.OptIn)]
    public class ToshootInstruction : SequenceItem, ISequenceItem{
        private IFramingAssistantVM framingAssistantVM;
        private ISequenceMediator sequenceMediator;
        private IDeepSkyObject deepSkyObject;        
        private ISequenceContainer sequenceContainer;
        private INighttimeCalculator nighttimeCalculator;
        private IProfileService profileService;
        private IApplicationMediator applicationMediator;
        private IPlanetariumFactory planetariumFactory;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        

        public InputTarget Target { get; set; }
                

        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor parameters and will be automatically injected on instantiation by the plugin loader
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - ISafetyMonitorMediator,
        ///     - IApplicationMediator
        ///     - IApplicationResourceDictionary
        ///     - IFramingAssistantVM
        ///     - IList<IDateTimeProvider>
        /// </remarks>
        [ImportingConstructor]
        public ToshootInstruction(IFramingAssistantVM framingAssistantVM, ISequenceMediator sequenceMediator, INighttimeCalculator nighttimeCalculator, IProfileService profileService, IApplicationMediator applicationMediator, IPlanetariumFactory planetariumFactory, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator) {
            this.framingAssistantVM = framingAssistantVM;
            this.sequenceMediator = sequenceMediator;
            this.nighttimeCalculator = nighttimeCalculator;
            Text = Properties.Settings.Default.DefaultNotificationMessage;
            this.profileService = profileService;   
            this.applicationMediator = applicationMediator;
            this.planetariumFactory = planetariumFactory;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            Target = new InputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon);



        }
        public ToshootInstruction(ToshootInstruction copyMe) : this(copyMe.framingAssistantVM, copyMe.sequenceMediator, copyMe.nighttimeCalculator, copyMe.profileService, copyMe.applicationMediator, copyMe.planetariumFactory, copyMe.cameraMediator, copyMe.filterWheelMediator) {
            CopyMetaData(copyMe);
        }
        
        
               
        /// <summary>
        /// An example property that can be set from the user interface via the Datatemplate specified in PluginTestItem.Template.xaml
        /// </summary>
        /// <remarks>
        /// If the property changes from the code itself, remember to call RaisePropertyChanged() on it for the User Interface to notice the change
        /// </remarks>
        [JsonProperty]
        public string Text { get; set; }
       

        /// <summary>
        /// The core logic when the sequence item is running resides here
        /// Add whatever action is necessary
        /// </summary>
        /// <param name="progress">The application status progress that can be sent back during execution</param>
        /// <param name="token">When a cancel signal is triggered from outside, this token can be used to register to it or check if it is cancelled</param>
        /// <returns></returns>
        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Notification.ShowSuccess(Text);
            // Add logic to run the item here


            //Crée le dossier pour enregistrer le fichier fini
            //Create the folder to save the finished file
            string folderPath = Text + "Sync";
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath); 
                Console.WriteLine(folderPath);
            }

            string directoryPath = Text;
            string[] files = null;


            //Attends jusqu'à ce qu'un fichier "toconf*.txt" apparaisse dans le répertoire spécifié
            //Wait until a file named 'toconf*.txt' appears in the specified directory
            while (files == null || files.Length == 0) {
                Console.WriteLine("En attente d'un fichier toconf*.txt dans le répertoire " + directoryPath);
                System.Threading.Thread.Sleep(1000); //Attend 1 seconde avant de vérifier à nouveau //Wait for 1 second before checking again
                files = Directory.GetFiles(directoryPath, "toconf*.txt");
            }

            string closestFile = null;
            int closestNumber = int.MaxValue;

            foreach (string file in files) {
                int number = int.Parse(Path.GetFileNameWithoutExtension(file).Substring(6));
                int difference = Math.Abs(number);
                if (difference < closestNumber) {
                    closestFile = file;
                    closestNumber = difference;
                }

                try {

                    //Ouvrir le fichier en lecture
                    //Open the file for reading
                    string directdoss = closestFile;
                    StreamReader fichier = new StreamReader(directdoss);


                    //Lire une ligne de texte depuis le fichier
                    //Read a line of text from the file
                    string ligne = fichier.ReadLine();


                    //Découper la ligne en utilisant la méthode Split
                    //Split the line using the Split method
                    string[] param = ligne.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);


                    //Nom du champ
                    //Field name
                    string namech = param[0];

                    //Coordonnées RA champ 
                    //Field RA coordinates
                    int RAh = int.Parse(param[4]);
                    int RAm = int.Parse(param[5]);
                    double RAs = double.Parse(param[6], CultureInfo.InvariantCulture);


                    //Convertis les champs en une valeur degree
                    //Convert the fields to a degree value
                    double ra = (RAh + (RAm / 60.0) + (RAs / 3600.0)) * 15.0;


                    //Renvoie l'angle en degree
                    //Return the angle in degrees
                    Angle raok = Angle.ByDegree(ra);


                    //Coordonnées DEC champ
                    //Field DEC coordinates
                    int DECd = int.Parse(param[7]);
                    int DECm = int.Parse(param[8]);
                    double DECs = double.Parse(param[9], CultureInfo.InvariantCulture);


                    //Recherche le signe devant la dec + ou -
                    //Look for the sign in front of the DEC, either + or -
                    double signe = Math.Sign(DECd);


                    //Convertis les champs en une valeur en degree
                    //Convert the fields to a degree value
                    double dec = signe * (Math.Abs(DECd) + (DECm / 60.0) + (DECs / 3600.0));


                    //Renvoie l'angle en degree
                    //Return the angle in degrees
                    Angle decok = Angle.ByDegree(dec);


                    //Renvoie les coordonness ra + dec
                    //Return the RA and DEC coordinates
                    Coordinates coords = new Coordinates(raok, decok, Epoch.J2000);


                    //Ecrit les valeurs dans le DSO de nina
                    //Write fewer values into the NINA DSO
                    ISequenceContainer parent = Parent; {
                        if (parent != null) { 
                            var dso = parent as IDeepSkyObjectContainer;
                            if (dso != null) {                                
                                dso.Target.InputCoordinates.Coordinates = coords;
                                dso.Target.TargetName = namech;
                            } 
                        }
                    }                                     
                                        

                    //Crée le fichier text de suivi de la soirée
                    //Create the text file for the evening log
                    string fileName = namech + ".txt";                    
                    File.WriteAllText(Path.Combine(Text, "Sync", fileName), $"{namech} {coords}");


                    //Fermer le fichier
                    //Close the file
                    fichier.Close();


                    //Supprimer le fichier
                    //Delete the file
                    if (File.Exists(directdoss)) {
                        File.Delete(directdoss);
                    }
                    break;
                } finally {
                }
            }

            return Task.CompletedTask;
            
        }

        

        /// <summary>
        /// When items are put into the sequence via the factory, the factory will call the clone method. Make sure all the relevant fields are cloned with the object.
        /// </summary>
        /// <returns></returns>
        /// 

        

        public override object Clone() {  
             
            return new ToshootInstruction(this);
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ToshootInstruction)}, Text: {Text}";
        }
    }
}
