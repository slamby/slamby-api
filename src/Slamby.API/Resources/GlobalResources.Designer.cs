﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Slamby.API.Resources {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///    A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class GlobalResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        internal GlobalResources() {
        }
        
        /// <summary>
        ///    Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Slamby.API.Resources.GlobalResources", typeof(GlobalResources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///    Overrides the current thread's CurrentUICulture property for all
        ///    resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to HTTP Header Content-Length is not set.
        /// </summary>
        public static string ContentLengthIsNotSet {
            get {
                return ResourceManager.GetString("ContentLengthIsNotSet", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to DataSet &apos;{0}&apos; not found!.
        /// </summary>
        public static string DataSet_0_NotFound {
            get {
                return ResourceManager.GetString("DataSet_0_NotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Missing &apos;{0}&apos; header!.
        /// </summary>
        public static string Missing_0_Header {
            get {
                return ResourceManager.GetString("Missing_0_Header", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to .
        /// </summary>
        public static string NGramList {
            get {
                return ResourceManager.GetString("NGramList", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NGram list contains duplicate values.
        /// </summary>
        public static string NGramListContainsDuplicates {
            get {
                return ResourceManager.GetString("NGramListContainsDuplicates", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NGram list is empty.
        /// </summary>
        public static string NGramListIsEmpty {
            get {
                return ResourceManager.GetString("NGramListIsEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NGram values should be between 1 and 6.
        /// </summary>
        public static string NGramValuesShouldBeBetween1And6 {
            get {
                return ResourceManager.GetString("NGramValuesShouldBeBetween1And6", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Server resource error. Request entity is too large. Maximum value according to the server resources is {0} bytes.
        /// </summary>
        public static string Request_Is_Too_Large_0 {
            get {
                return ResourceManager.GetString("Request_Is_Too_Large_0", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to SDK {0} &lt;--&gt; API {1} version mismatch.
        /// </summary>
        public static string SdkApiVersionMismatch {
            get {
                return ResourceManager.GetString("SdkApiVersionMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to API Secret is not set! Please visit {0} to complete the setup process..
        /// </summary>
        public static string SecretIsNotSetVisit_0_ForSetup {
            get {
                return ResourceManager.GetString("SecretIsNotSetVisit_0_ForSetup", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Secret must be at least {0} characters long..
        /// </summary>
        public static string SecretMustBeAtLeast_0_CharactersLong {
            get {
                return ResourceManager.GetString("SecretMustBeAtLeast_0_CharactersLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The argument {0} is not {1} type.
        /// </summary>
        public static string TheArgument_0_IsNot_1_Type {
            get {
                return ResourceManager.GetString("TheArgument_0_IsNot_1_Type", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The argument cannot be null: {0}.
        /// </summary>
        public static string TheArgumentCannotBeNull_0 {
            get {
                return ResourceManager.GetString("TheArgumentCannotBeNull_0", resourceCulture);
            }
        }
    }
}
