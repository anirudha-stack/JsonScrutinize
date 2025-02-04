using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JsonScrutinize
{
    public class JsonScrutinize
    {
        private readonly JsonHelper _jsonHelper = new JsonHelper();

        /// <summary>
        /// Traverses the input JSON along with the specified comparison type to identify mismatched types.
        /// Returns an object with codes indicating success or error.
        /// </summary>
        /// <param name="inputJson">The JSON string to validate.</param>
        /// <param name="typeToCompare">The type against which the JSON is validated.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of result codes.</returns>
        public Task<KeyTypeScrutinizeResult> KeyTypeScrutinize(string inputJson, Type typeToCompare)
        {
            return _jsonHelper.KeyTypeScrutinize(inputJson, typeToCompare);
        }

        /// <summary>
        /// Traverses the input JSON along with the specified comparison type to identify null keys.
        /// Returns an object with codes indicating success or error.
        /// </summary>
        /// <param name="inputJson">The JSON string to validate.</param>
        /// <param name="typeToCompare">The type against which the JSON is validated.</param>
        /// <param name="optionalProperties">
        /// List of property names to ignore when checking for null values.
        /// </param>
        /// <returns>A task representing the asynchronous operation, containing a list of result codes.</returns>
        public Task<KeyNullScrutinizeResult> KeyNullScrutinize(string inputJson, Type typeToCompare,params string[] optionalKeys)
        {
            return _jsonHelper.KeyNullScrutinize(inputJson, typeToCompare,optionalKeys);
        }


        /// <summary>
        /// Traverses the input JSON along with the specified comparison type to identify missing keys.
        /// Returns an object with codes indicating success or error.
        /// </summary>
        /// <param name="inputJson">The JSON string to validate.</param>
        /// <param name="typeToCompare">The type against which the JSON is validated.</param>
        /// <param name="optionalProperties">
        /// List of property names to ignore when checking for missing values.
        /// </param>
        /// <returns>A task representing the asynchronous operation, containing a list of result codes.</returns>
        public Task<KeyMissingScrutinizeResult> KeyMissingScrutinize(string inputJson, Type typeToCompare, params string[] optionalKeys)
        {
            return _jsonHelper.KeyMissingScrutinize(inputJson, typeToCompare, optionalKeys);
        }



    }
}
